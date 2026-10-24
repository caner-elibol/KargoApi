using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Kargo.Application.Search;
using KargoApi.Search;

namespace Kargo.Infrastructure.Search;

public class ShipmentSearch(ElasticsearchClient es) : IShipmentSearch
{
    public async Task PingAsync(CancellationToken ct = default)
    {
        var response = await es.PingAsync(ct);
        if (!response.IsValidResponse)
            throw new SearchUnavailableException(response.DebugInformation);
    }

    public async Task<ShipmentSearchResult> SearchAsync(ShipmentSearchQuery query, CancellationToken ct = default)
    {
        var must = new List<Query>();
        var filter = new List<Query>();
        var mustNot = new List<Query>();

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            must.Add(new MatchQuery("senderName")
            {
                Query = query.Q,
                Fuzziness = new Fuzziness("AUTO"),
                Operator = Operator.And
            });
        }

        if (!string.IsNullOrWhiteSpace(query.City))
            filter.Add(new TermQuery("receiverCity") { Value = query.City });

        if (query.Delivered == true)
            filter.Add(new ExistsQuery { Field = "deliveredAt" });
        else if (query.Delivered == false)
            mustNot.Add(new ExistsQuery { Field = "deliveredAt" });

        var request = new SearchRequest("shipments")
        {
            From = (query.Page - 1) * query.PageSize,
            Size = query.PageSize,
            Query = new BoolQuery { Must = must, Filter = filter, MustNot = mustNot },
            Aggregations = new Dictionary<string, Aggregation>
            {
                ["durumlar"] = new TermsAggregation { Field = "status" }
            }
        };

        var response = await es.SearchAsync<ShipmentDocument>(request, ct);

        if (!response.IsValidResponse)
            throw new SearchUnavailableException(response.DebugInformation);

        var hits = response.Hits
            .Select(h => new ShipmentHit(h.Id!, h.Score, h.Source!))
            .ToList();

        var statusCounts = response.Aggregations?
            .GetLongTerms("durumlar")?
            .Buckets
            .Select(b => new StatusCount(b.Key, b.DocCount))
            .ToList() ?? [];

        return new ShipmentSearchResult(response.Total, hits, statusCounts);
    }
}