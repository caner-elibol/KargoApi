using Kargo.Domain.Enums;

namespace Kargo.Domain;

public sealed record ShipmentStatusChanged(int ShipmentId, ShipmentStatus NewStatus) : IDomainEvent;