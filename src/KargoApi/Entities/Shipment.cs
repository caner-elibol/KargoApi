using KargoApi.Entities.Enums;

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
        public ShipmentStatus Status { get; set; }
        public decimal WeightKg { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }

        public void StartTransit()
        {
            EnsureCanTransitionTo(ShipmentStatus.InTransit);
            Status = ShipmentStatus.InTransit;
        }

        public void SendOutForDelivery()
        {
            EnsureCanTransitionTo(ShipmentStatus.OutForDelivery);
            Status = ShipmentStatus.OutForDelivery;
        }

        public void MarkAsDelivered(DateTime deliveredAt)
        {
            EnsureCanTransitionTo(ShipmentStatus.Delivered);
            Status = ShipmentStatus.Delivered;
            DeliveredAt = deliveredAt;
        }

        public void Return()
        {
            EnsureCanTransitionTo(ShipmentStatus.Returned);
            Status = ShipmentStatus.Returned;
        }

        public void MarkAsLost()
        {
            EnsureCanTransitionTo(ShipmentStatus.Lost);
            Status = ShipmentStatus.Lost;
        }

        private void EnsureCanTransitionTo(ShipmentStatus target)
        {
            var allowed = Status switch
            {
                ShipmentStatus.Accepted => new[] { ShipmentStatus.InTransit, ShipmentStatus.Lost },
                ShipmentStatus.InTransit => new[] { ShipmentStatus.OutForDelivery, ShipmentStatus.Returned, ShipmentStatus.Lost },
                ShipmentStatus.OutForDelivery => new[] { ShipmentStatus.Delivered, ShipmentStatus.InTransit, ShipmentStatus.Returned, ShipmentStatus.Lost },
                _ => Array.Empty<ShipmentStatus>()
            };

            if (!allowed.Contains(target))
                throw new DomainException($"Gönderi '{Status}' durumundan '{target}' durumuna geçemez.");
        }
    }
}
