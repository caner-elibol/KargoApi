namespace KargoApi.Search
{
    public record ShipmentHit(string Id, double? Score, ShipmentDocument Shipment);

    public record StatusCount(long Status, long Count);

    public record ShipmentSearchResult(
        long Total,
        IReadOnlyList<ShipmentHit> Hits,
        IReadOnlyList<StatusCount> StatusCounts);
}
