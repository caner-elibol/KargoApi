namespace Kargo.Infrastructure.Data
{
    public class SearchOutbox
    {
        public long Id { get; set; }
        public Guid ShipmentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
}
