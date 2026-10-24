namespace Kargo.Application.Search;

public interface IShipmentIndexer
{
    Task<int> IndexAllAsync(string indexName, CancellationToken ct = default);
}