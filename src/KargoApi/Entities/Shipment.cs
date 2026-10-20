namespace KargoApi.Entities
{
    public class Shipment
    {
        public int Id { get; set; }
        public string TrackingNo { get; set; } = null!;
        public string SenderName { get; set; } = null!;
        public string ReceiverCity { get; set; } = null!;
        public int OriginBranchId { get; set; }
        public Branch OriginBranch { get; set; } = null!;
        public byte Status { get; set; }
        public decimal WeightKg { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
    }
}
