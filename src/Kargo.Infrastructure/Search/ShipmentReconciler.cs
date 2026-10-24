using System.Text;
using System.Text.Json;
using Kargo.Application.Search;
using Kargo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kargo.Infrastructure.Search;

public class ShipmentReconciler(
    AppDbContext db,
    IConfiguration config,
    ILogger<ShipmentReconciler> logger) : ISearchReconciler
{
    private const string Alias = "shipments";
    private const int SqlBatch = 10_000;
    private const int CheckBatch = 1_000;

    public async Task<ReconciliationReport> ReconcileAsync(
        bool dryRun = true, int maxFixes = 50_000, CancellationToken ct = default)
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(config["Elasticsearch:Url"]!),
            Timeout = TimeSpan.FromMinutes(5)
        };

        // 1) Elasticsearch'teki tüm Id'ler
        var esIds = await LoadEsIdsAsync(http, ct);
        var esCount = esIds.Count;

        // 2) SQL'i keyset ile gez: ES'te yoksa "eksik", varsa kümeden çıkar
        var missing = new List<Guid>();
        var sqlCount = 0;
        var last = Guid.Empty;

        while (true)
        {
            var ids = await db.Shipments
                .AsNoTracking()
                .Where(s => s.Id.CompareTo(last) > 0)
                .OrderBy(s => s.Id)
                .Select(s => s.Id)
                .Take(SqlBatch)
                .ToListAsync(ct);

            if (ids.Count == 0) break;

            foreach (var id in ids)
                if (!esIds.Remove(id))
                    missing.Add(id);

            sqlCount += ids.Count;
            last = ids[^1];
        }

        // 3) Kümede kalanlar "hayalet" (ES'te var, SQL'de yok)
        var orphans = esIds.ToList();

        // 4) Yanlış alarm koruması: aday olanları ikinci kez kontrol et
        missing = await ConfirmMissingAsync(http, missing, ct);
        orphans = await ConfirmOrphansAsync(orphans, ct);

        // 5) Güvenlik sınırı + outbox'a yazma
        var total = missing.Count + orphans.Count;
        var limitExceeded = total > maxFixes;
        var enqueued = 0;

        if (!dryRun && !limitExceeded)
        {
            foreach (var chunk in missing.Concat(orphans).Chunk(1_000))
            {
                // NOT: SearchOutbox'ta zorunlu başka alan varsa (CreatedAt, tür vb.) burada ver.
                db.SearchOutbox.AddRange(chunk.Select(id => new SearchOutbox { ShipmentId = id }));
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
                enqueued += chunk.Length;
            }
        }

        logger.LogInformation(
            "Mutabakat: SQL={Sql}, ES={Es}, eksik={Missing}, hayalet={Orphans}, dryRun={DryRun}, limitAşıldı={Limit}, kuyruğaAlınan={Enqueued}",
            sqlCount, esCount, missing.Count, orphans.Count, dryRun, limitExceeded, enqueued);

        return new ReconciliationReport(
            sqlCount, esCount, missing.Count, orphans.Count,
            missing.Take(20).ToList(), orphans.Take(20).ToList(),
            dryRun, limitExceeded, enqueued);
    }

    // ---- Elasticsearch: tüm _id'leri PIT + search_after ile çek ----
    private static async Task<HashSet<Guid>> LoadEsIdsAsync(HttpClient http, CancellationToken ct)
    {
        var pitResp = await http.PostAsync($"{Alias}/_pit?keep_alive=2m", null, ct);
        pitResp.EnsureSuccessStatusCode();

        string pitId;
        using (var d = JsonDocument.Parse(await pitResp.Content.ReadAsStringAsync(ct)))
            pitId = d.RootElement.GetProperty("id").GetString()!;

        var ids = new HashSet<Guid>();
        long? after = null;

        try
        {
            while (true)
            {
                var req = new Dictionary<string, object?>
                {
                    ["size"] = 10_000,
                    ["_source"] = false,
                    ["track_total_hits"] = false,
                    ["pit"] = new { id = pitId, keep_alive = "2m" },
                    ["sort"] = new object[] { new { _shard_doc = "asc" } }
                };
                if (after is not null)
                    req["search_after"] = new[] { after.Value };

                var resp = await http.PostAsync("_search",
                    new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"), ct);
                resp.EnsureSuccessStatusCode();

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var hits = doc.RootElement.GetProperty("hits").GetProperty("hits");
                if (hits.GetArrayLength() == 0) break;

                foreach (var h in hits.EnumerateArray())
                {
                    if (Guid.TryParse(h.GetProperty("_id").GetString(), out var g))
                        ids.Add(g);
                    after = h.GetProperty("sort")[0].GetInt64();
                }

                if (doc.RootElement.TryGetProperty("pit_id", out var np))
                    pitId = np.GetString()!;
            }
        }
        finally
        {
            try
            {
                var close = new HttpRequestMessage(HttpMethod.Delete, "_pit")
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { id = pitId }),
                        Encoding.UTF8, "application/json")
                };
                await http.SendAsync(close, CancellationToken.None);
            }
            catch { /* PIT kendiliğinden zaman aşımına uğrar */ }
        }

        return ids;
    }

    // "Eksik" adaylar: bekleyen outbox satırı varsa yoldadır (atla); ES'te artık varsa eksik değildir
    private async Task<List<Guid>> ConfirmMissingAsync(HttpClient http, List<Guid> candidates, CancellationToken ct)
    {
        var result = new List<Guid>();

        foreach (var chunk in candidates.Chunk(CheckBatch))
        {
            var ids = chunk.ToList();

            var pending = await db.SearchOutbox
                .AsNoTracking()
                .Where(o => o.ProcessedAt == null && ids.Contains(o.ShipmentId))
                .Select(o => o.ShipmentId)
                .Distinct()
                .ToListAsync(ct);

            var stillMissing = ids.Except(pending).ToList();
            if (stillMissing.Count == 0) continue;

            var found = await EsExistingAsync(http, stillMissing, ct);
            result.AddRange(stillMissing.Where(id => !found.Contains(id)));
        }

        return result;
    }

    // "Hayalet" adaylar: SQL'de artık varsa hayalet değildir (tarama sırasında oluşmuş olabilir)
    private async Task<List<Guid>> ConfirmOrphansAsync(List<Guid> candidates, CancellationToken ct)
    {
        var result = new List<Guid>();

        foreach (var chunk in candidates.Chunk(CheckBatch))
        {
            var ids = chunk.ToList();

            var inSql = await db.Shipments
                .AsNoTracking()
                .Where(s => ids.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync(ct);

            result.AddRange(ids.Except(inSql));
        }

        return result;
    }

    private static async Task<HashSet<Guid>> EsExistingAsync(HttpClient http, List<Guid> ids, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new { ids = ids.Select(i => i.ToString()) });
        var resp = await http.PostAsync($"{Alias}/_mget?_source=false",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var found = new HashSet<Guid>();

        foreach (var d in doc.RootElement.GetProperty("docs").EnumerateArray())
        {
            if (d.TryGetProperty("found", out var f) && f.GetBoolean()
                && Guid.TryParse(d.GetProperty("_id").GetString(), out var g))
                found.Add(g);
        }

        return found;
    }
}