using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using KargoApi.Data;
using KargoApi.Entities;
using KargoApi.Search;
using KargoApi.Search.Results;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
{
    o.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sql => sql.EnableRetryOnFailure());

    if (builder.Environment.IsDevelopment())
    {
        o.LogTo(Console.WriteLine,
                new[] { DbLoggerCategory.Database.Command.Name },
                LogLevel.Information)
         .EnableSensitiveDataLogging();
    }
});

builder.Services.AddSingleton(_ =>
    new ElasticsearchClient(
        new ElasticsearchClientSettings(
            new Uri(builder.Configuration["Elasticsearch:Url"]!))));

builder.Services.AddHostedService<SearchOutboxWorker>();

var app = builder.Build();

// Sadece geliştirme ortamı için: uygulama açılırken migration'ları uygular
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.MapGet("/branches", async (AppDbContext db) =>
    await db.Branches.AsNoTracking().ToListAsync());

app.MapGet("/shipments", async (AppDbContext db, int page = 1, int pageSize = 20) =>
{
    pageSize = Math.Clamp(pageSize, 1, 100);

    return await db.Shipments
        .OrderByDescending(s => s.CreatedAt)
        .ThenByDescending(s => s.Id)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(s => new { s.Id, s.TrackingNo, s.Status, s.CreatedAt })
        .ToListAsync();
});

app.MapGet("/shipments/{trackingNo}", async (AppDbContext db, string trackingNo) =>
{
    var shipment = await db.Shipments
        .AsNoTracking()
        .FirstOrDefaultAsync(s => s.TrackingNo == trackingNo);

    return shipment is null ? Results.NotFound() : Results.Ok(shipment);
});

app.MapGet("/shipments/{trackingNo}/status", async (AppDbContext db, string trackingNo) =>
{
    var result = await db.Shipments
        .Where(s => s.TrackingNo == trackingNo)
        .Select(s => new { s.TrackingNo, s.Status, s.CreatedAt })
        .FirstOrDefaultAsync();

    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapGet("/stats/status/{status}", async (AppDbContext db, byte status) =>
{
    var stats = await db.Database
        .SqlQuery<StatusStats>($"EXEC dbo.GetShipmentStatsByStatus @Status = {status}")
        .ToListAsync();

    return stats.FirstOrDefault();
});

app.MapPost("/branches", async (AppDbContext db, Branch branch) =>
{
    db.Branches.Add(branch);
    await db.SaveChangesAsync();
    return Results.Created($"/branches/{branch.Id}", branch);
});
app.MapPost("/demo/concurrency", async (AppDbContext db) =>
{
    // Ali şubeyi okuyor
    var branch = await db.Branches.FirstAsync(b => b.Id == 1);

    // Ayşe araya giriyor ve aynı satırı değiştiriyor
    await db.Database.ExecuteSqlAsync(
        $"UPDATE dbo.Branches SET Name = N'Ayşe değiştirdi' WHERE Id = 1");

    // Ali kendi değişikliğini kaydetmeye çalışıyor
    branch.Name = "Ali değiştirdi";

    try
    {
        await db.SaveChangesAsync();
        return Results.Ok("Kaydedildi");
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict("Bu kayıt siz okuduktan sonra başkası tarafından değiştirildi.");
    }
});
app.MapPost("/demo/transaction", async (AppDbContext db, int originBranchId) =>
{
    var strategy = db.Database.CreateExecutionStrategy();

    return await strategy.ExecuteAsync(async () =>
    {
        db.ChangeTracker.Clear();

        await using var tx = await db.Database.BeginTransactionAsync();

        var branch = await db.Branches.FirstAsync(b => b.Id == 1);
        branch.Name = "Kadıköy Şube - TEST";
        await db.SaveChangesAsync();

        db.Shipments.Add(new Shipment
        {
            TrackingNo = "EF001",
            SenderName = "Test",
            ReceiverCity = "Ankara",
            OriginBranchId = originBranchId,
            Status = 1,
            WeightKg = 1m,
            CreatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        await tx.CommitAsync();
        return Results.Ok("Tamamlandı");
    });
});

app.MapGet("/es/ping", async (ElasticsearchClient es) =>
{
    var response = await es.PingAsync();
    return response.IsValidResponse
        ? "Elasticsearch bağlantısı tamam"
        : response.DebugInformation;
});

app.MapPut("/shipments/{id:int}/status", async (int id, byte status, AppDbContext db) =>
{
    var shipment = await db.Shipments.FindAsync(id);
    if (shipment is null) return Results.NotFound();

    shipment.Status = status;
    db.SearchOutbox.Add(new SearchOutbox { ShipmentId = id });

    await db.SaveChangesAsync();

    return Results.Ok("Gönderi güncellendi");
});

app.MapPost("/search/reindex", async (AppDbContext db, ElasticsearchClient es, ILogger<Program> logger) =>
{
    const int batchSize = 5000;
    var lastId = 0;
    var total = 0;
    var stopwatch = Stopwatch.StartNew();

    while (true)
    {
        var batch = await db.Shipments
            .AsNoTracking()
            .Where(s => s.Id > lastId)
            .OrderBy(s => s.Id)
            .Take(batchSize)
            .ToListAsync();

        if (batch.Count == 0) break;

        var docs = batch.Select(ShipmentDocument.From).ToList();

        var response = await es.BulkAsync(b => b
            .Index("shipments-v3")
            .IndexMany(docs, (op, doc) => op.Id(doc.Id.ToString())));

        if (!response.IsValidResponse || response.Errors)
        {
            var firstError = response.ItemsWithErrors.FirstOrDefault();
            return Results.Problem(firstError?.Error?.Reason ?? response.DebugInformation);
        }

        total += docs.Count;
        lastId = batch[^1].Id;
        logger.LogInformation("{Total} gönderi aktarıldı", total);
    }

    return Results.Ok(new { total, sure = stopwatch.Elapsed.ToString(@"mm\:ss") });
});
app.MapGet("/search/sql", async (AppDbContext db, string q) =>
{
    var stopwatch = Stopwatch.StartNew();

    var total = await db.Shipments
        .Where(s => s.SenderName.Contains(q))
        .CountAsync();

    return Results.Ok(new { total, sureMs = stopwatch.ElapsedMilliseconds });
});
app.MapGet("/search/shipments", async (
    ElasticsearchClient es,
    string? q,
    string? city,
    bool? delivered,
    int page = 1,
    int pageSize = 20) =>
{
    pageSize = Math.Clamp(pageSize, 1, 100);
    page = Math.Max(page, 1);

    var must = new List<Query>();
    var filter = new List<Query>();
    var mustNot = new List<Query>();

    if (!string.IsNullOrWhiteSpace(q))
    {
        must.Add(new MatchQuery("senderName")
        {
            Query = q,
            Fuzziness = new Fuzziness("AUTO"),
            Operator = Operator.And
        });
    }

    if (!string.IsNullOrWhiteSpace(city))
    {
        filter.Add(new TermQuery("receiverCity") { Value = city });
    }

    if (delivered == true)
        filter.Add(new ExistsQuery { Field = "deliveredAt" });
    else if (delivered == false)
        mustNot.Add(new ExistsQuery { Field = "deliveredAt" });

    var request = new SearchRequest("shipments")
    {
        From = (page - 1) * pageSize,
        Size = pageSize,
        Query = new BoolQuery { Must = must, Filter = filter, MustNot = mustNot },
        Aggregations = new Dictionary<string, Aggregation>
        {
            ["durumlar"] = new TermsAggregation { Field = "status" }
        },
        TrackTotalHits = new TrackHits(true),
    };

    var response = await es.SearchAsync<ShipmentDocument>(request);

    if (!response.IsValidResponse)
        return Results.Problem(response.DebugInformation);

    var hits = response.Hits
        .Select(h => new ShipmentHit(h.Id!, h.Score, h.Source!))
        .ToList();

    var statusCounts = response.Aggregations?
        .GetLongTerms("durumlar")?
        .Buckets
        .Select(b => new StatusCount(b.Key, b.DocCount))
        .ToList() ?? new List<StatusCount>();

    return Results.Ok(new ShipmentSearchResult(response.Total, hits, statusCounts));
});


app.Run();

public class StatusStats
{
    public int Adet { get; set; }
    public decimal? ToplamKg { get; set; }
}