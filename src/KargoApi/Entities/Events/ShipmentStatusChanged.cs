using KargoApi.Entities.Enums;

namespace KargoApi.Entities;

public sealed record ShipmentStatusChanged(int ShipmentId, ShipmentStatus NewStatus) : IDomainEvent;