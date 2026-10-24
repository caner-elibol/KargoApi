using Kargo.Application.Search;
using Kargo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace KargoApi.Search;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/es/ping", Ping).WithTags("Search");

        var group = app.MapGroup("/search").WithTags("Search");

        group.MapGet("/shipments", SearchShipments);
        group.MapPost("/reindex", Reindex);
        group.MapGet("/sql", SearchSql);

        return app;
    }

    private static async Task<IResult> Ping(IShipmentSearch search, CancellationToken ct)
    {
        try
        {
            await search.PingAsync(ct);
            return Results.Ok("Elasticsearch bağlantısı tamam");
        }
        catch (SearchUnavailableException ex)
        {
            return Results.Problem(ex.Message);
        }
    }
    private static async Task<IResult> SearchShipments(
        IShipmentSearch search,
        string? q,
        string? city,
        bool? delivered,
        CancellationToken ct,
        int page = 1,
        int pageSize = 20)
    {
        var query = new ShipmentSearchQuery(
            q, city, delivered,
            Math.Max(page, 1),
            Math.Clamp(pageSize, 1, 100));

        try
        {
            return Results.Ok(await search.SearchAsync(query, ct));
        }
        catch (SearchUnavailableException ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> Reindex(IShipmentIndexer indexer)
    {
        var stopwatch = Stopwatch.StartNew();
        var total = await indexer.IndexAllAsync("shipments-v3");
        return Results.Ok(new { total, sure = stopwatch.Elapsed.ToString(@"mm\:ss") });
    }

    private static async Task<IResult> SearchSql(AppDbContext db, string q)
    {
        var stopwatch = Stopwatch.StartNew();

        var total = await db.Shipments
            .Where(s => s.SenderName.Contains(q))
            .CountAsync();

        return Results.Ok(new { total, sureMs = stopwatch.ElapsedMilliseconds });
    }
}