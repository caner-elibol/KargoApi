using KargoApi.Search;

namespace Kargo.Application.Search;

public record ShipmentSearchQuery(string? Q, string? City, bool? Delivered, int Page, int PageSize);

public class SearchUnavailableException(string message) : Exception(message);

public interface IShipmentSearch
{
    Task PingAsync(CancellationToken ct = default);
    Task<ShipmentSearchResult> SearchAsync(ShipmentSearchQuery query, CancellationToken ct = default);
}