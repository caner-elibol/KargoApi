using Elastic.Clients.Elasticsearch;
using KargoApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KargoApi.Search;

public class SearchOutboxWorker(
    IServiceScopeFactory scopeFactory,
    ElasticsearchClient es,
    ILogger<SearchOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);

                if (processed == 0)
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox işlenirken hata oluştu, 10 saniye sonra tekrar denenecek");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. Bekleyen kayıtları al
        var pending = await db.SearchOutbox
            .Where(o => o.ProcessedAt == null)
            .OrderBy(o => o.Id)
            .Take(500)
            .ToListAsync(ct);

        if (pending.Count == 0) return 0;

        // 2. Gönderilerin güncel halini SQL Server'dan oku
        var shipmentIds = pending.Select(o => o.ShipmentId).Distinct().ToList();

        var shipments = await db.Shipments
            .AsNoTracking()
            .Where(s => shipmentIds.Contains(s.Id))
            .ToListAsync(ct);

        // 3. Elasticsearch'e yaz
        var docs = shipments.Select(ShipmentDocument.From).ToList();

        if (docs.Count > 0)
        {
            var response = await es.BulkAsync(b => b
                .Index("shipments")
                .IndexMany(docs, (op, doc) => op.Id(doc.Id.ToString())), ct);

            if (!response.IsValidResponse || response.Errors)
            {
                var reason = response.ItemsWithErrors.FirstOrDefault()?.Error?.Reason
                             ?? response.DebugInformation;
                throw new InvalidOperationException($"Elasticsearch güncellenemedi: {reason}");
            }
        }

        // 4. Kayıtları işlendi olarak işaretle
        var outboxIds = pending.Select(o => o.Id).ToList();
        var now = DateTime.Now;

        await db.SearchOutbox
            .Where(o => outboxIds.Contains(o.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.ProcessedAt, now), ct);

        logger.LogInformation("{Count} outbox kaydı işlendi", pending.Count);
        return pending.Count;
    }
}