using KargoApi.Data;
using KargoApi.Entities;
using KargoApi.Entities.Enums;
using KargoApi.Features.Branches;
using KargoApi.Features.Shipments;
using KargoApi.Search;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace KargoApi.Features.Shipments
{
    public static class ShipmentEndpoints
    {
        public static IEndpointRouteBuilder MapShipmentEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/shipments").WithTags("Shipments");

            group.MapGet("/", GetPage);
            group.MapGet("/{trackingNo}", GetByTrackingNo);
            group.MapGet("/{trackingNo}/status", GetStatus);
            group.MapPut("/{id:int}/status", UpdateStatus);

            return app;
        }

        private static async Task<IResult> GetPage(AppDbContext db, int page = 1, int pageSize = 20)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);

            var items = await db.Shipments
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new { s.Id, s.TrackingNo, s.Status, s.CreatedAt })
                .ToListAsync();

            return Results.Ok(items);
        }
       

        private static async Task<IResult> GetByTrackingNo(AppDbContext db, string trackingNo)
        {
            if (!TrackingNumber.TryCreate(trackingNo, out var number, out var error))
            {
                return Results.Problem(
                    title: "Geçersiz takip numarası",
                    detail: error,
                    statusCode: StatusCodes.Status400BadRequest);
            }
            var shipment = await db.Shipments
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TrackingNo == number);

            return shipment is null ? Results.NotFound() : Results.Ok(shipment);
        }

        private static async Task<IResult> GetStatus(AppDbContext db, string trackingNo)
        {
            if (!TrackingNumber.TryCreate(trackingNo, out var number, out var error))
                return Results.Problem(
                    title: "Geçersiz takip numarası",
                    detail: error,
                    statusCode: StatusCodes.Status400BadRequest);

            var result = await db.Shipments
                .Where(s => s.TrackingNo == number)
                .Select(s => new { TrackingNo = s.TrackingNo.Value, s.Status, s.CreatedAt })
                .FirstOrDefaultAsync();

            return result is null ? Results.NotFound() : Results.Ok(result);

        }
        

        private static async Task<IResult> UpdateStatus(
    int id, byte status, AppDbContext db)
        {
            var shipment = await db.Shipments.FindAsync(id);
            if (shipment is null) return Results.NotFound();

            try
            {
                switch ((ShipmentStatus)status)
                {
                    case ShipmentStatus.InTransit:
                        shipment.StartTransit();
                        break;
                    case ShipmentStatus.OutForDelivery:
                        shipment.SendOutForDelivery();
                        break;
                    case ShipmentStatus.Delivered:
                        shipment.MarkAsDelivered(DateTime.Now);
                        break;
                    case ShipmentStatus.Returned:
                        shipment.Return();
                        break;
                    case ShipmentStatus.Lost:
                        shipment.MarkAsLost();
                        break;
                    default:
                        return Results.Problem(
        title: "Geçersiz hedef durum",
        detail: $"'{status}' geçerli bir gönderi durumu değil.",
        statusCode: StatusCodes.Status400BadRequest);
                }
            }
            catch (DomainException ex)
            {
                return Results.Problem(
                    title: "Geçersiz durum geçişi",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                shipment.Id,
                shipment.TrackingNo,
                shipment.Status,
                shipment.DeliveredAt
            });
        }
    }

}