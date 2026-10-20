using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;
using KargoApi.Data;
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

    private static async Task<IResult> Ping(ElasticsearchClient es)
    {
        var response = await es.PingAsync();
        return response.IsValidResponse
            ? Results.Ok("Elasticsearch bağlantısı tamam")
            : Results.Problem(response.DebugInformation);
    }

    private static async Task<IResult> SearchShipments(
        ElasticsearchClient es,
        string? q,
        string? city,
        bool? delivered,
        int page = 1,
        int pageSize = 20)
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
            filter.Add(new TermQuery("receiverCity") { Value = city });

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
            }
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
    }

    private static async Task<IResult> Reindex(ShipmentIndexer indexer)
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