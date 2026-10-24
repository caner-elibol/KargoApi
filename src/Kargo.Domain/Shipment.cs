using Kargo.Domain.Enums;
using Kargo.Domain.Events;

namespace Kargo.Domain
{
    public class Shipment: AggregateRoot
    {
        public Guid Id { get; private set; }
        public TrackingNumber TrackingNo { get; private set; } = null!;
        public string SenderName { get; set; } = null!;
        public string ReceiverCity { get; set; } = null!;
        public int OriginBranchId { get; set; }
        public Branch OriginBranch { get; set; } = null!;
        public ShipmentStatus Status { get; private set; }
        public decimal WeightKg { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeliveredAt { get; private set; }
        public static Shipment Create(
            TrackingNumber trackingNo,
            string senderName,
            string receiverCity,
            int originBranchId,
            decimal weightKg,
            DateTime createdAt)
        {
            if (weightKg <= 0)
                throw new DomainException("Gönderi ağırlığı sıfırdan büyük olmalı.");
            var shipment = new Shipment
            {
                Id = Guid.CreateVersion7(),
                TrackingNo = trackingNo,
                SenderName = senderName,
                ReceiverCity = receiverCity,
                OriginBranchId = originBranchId,
                WeightKg = weightKg,
                CreatedAt = createdAt,
                Status = ShipmentStatus.Accepted
            };
            shipment.AddDomainEvent(new ShipmentStatusChanged(shipment.Id, shipment.Status));
            return shipment;
        }

        public void StartTransit()
        {
            EnsureCanTransitionTo(ShipmentStatus.InTransit);
            Status = ShipmentStatus.InTransit;
            AddDomainEvent(new ShipmentStatusChanged(Id, Status));
        }

        public void SendOutForDelivery()
        {
            EnsureCanTransitionTo(ShipmentStatus.OutForDelivery);
            Status = ShipmentStatus.OutForDelivery;
            AddDomainEvent(new ShipmentStatusChanged(Id, Status));
        }

        public void MarkAsDelivered(DateTime deliveredAt)
        {
            EnsureCanTransitionTo(ShipmentStatus.Delivered);
            Status = ShipmentStatus.Delivered;
            DeliveredAt = deliveredAt;
            AddDomainEvent(new ShipmentStatusChanged(Id, Status));
        }

        public void Return()
        {
            EnsureCanTransitionTo(ShipmentStatus.Returned);
            Status = ShipmentStatus.Returned;
            AddDomainEvent(new ShipmentStatusChanged(Id, Status));
        }

        public void MarkAsLost()
        {
            EnsureCanTransitionTo(ShipmentStatus.Lost);
            Status = ShipmentStatus.Lost;
            AddDomainEvent(new ShipmentStatusChanged(Id, Status));
        }

        private void EnsureCanTransitionTo(ShipmentStatus target)
        {
            var allowed = Status switch
            {
                ShipmentStatus.Accepted => [ShipmentStatus.InTransit, ShipmentStatus.Lost],
                ShipmentStatus.InTransit => [ShipmentStatus.OutForDelivery, ShipmentStatus.Returned, ShipmentStatus.Lost],
                ShipmentStatus.OutForDelivery => [ShipmentStatus.Delivered, ShipmentStatus.InTransit, ShipmentStatus.Returned, ShipmentStatus.Lost],
                _ => Array.Empty<ShipmentStatus>()
            };

            if (!allowed.Contains(target))
                throw new DomainException($"Gönderi '{Status}' durumundan '{target}' durumuna geçemez.");
        }
    }
}
