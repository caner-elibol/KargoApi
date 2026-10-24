namespace Kargo.Domain.Enums;

public enum ShipmentStatus : byte
{
    Accepted = 1,        // Kabul edildi
    InTransit = 2,       // Yolda
    OutForDelivery = 3,  // Dağıtımda
    Delivered = 4,       // Teslim edildi
    Returned = 5,        // İade
    Lost = 6             // Kayıp
}