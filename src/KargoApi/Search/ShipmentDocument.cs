using KargoApi.Entities;
using System.Text.Json.Serialization;

namespace KargoApi.Search
{
    public class ShipmentDocument
    {
        [JsonIgnore]
        public int Id { get; set; }

        public string TrackingNo { get; set; } = null!;
        public string SenderName { get; set; } = null!;
        public string ReceiverCity { get; set; } = null!;
        public int OriginBranchId { get; set; }
        public byte Status { get; set; }
        public decimal WeightKg { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? DeliveredAt { get; set; }

        private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

        public static ShipmentDocument From(Shipment s) => new()
        {
            Id = s.Id,
            TrackingNo = s.TrackingNo.Value,
            SenderName = s.SenderName,
            ReceiverCity = s.ReceiverCity,
            OriginBranchId = s.OriginBranchId,
            Status = (byte)s.Status,
            WeightKg = s.WeightKg,
            CreatedAt = new DateTimeOffset(s.CreatedAt, TurkeyOffset),
            DeliveredAt = s.DeliveredAt is null
                ? null
                : new DateTimeOffset(s.DeliveredAt.Value, TurkeyOffset)
        };
    }
}
