using Kargo.Domain.Enums;

namespace Kargo.Domain.Events;

public sealed record ShipmentStatusChanged(Guid ShipmentId, ShipmentStatus NewStatus) : IDomainEvent;