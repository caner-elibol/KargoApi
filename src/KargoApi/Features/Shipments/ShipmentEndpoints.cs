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
            var shipment = await db.Shipments
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TrackingNo == trackingNo);

            return shipment is null ? Results.NotFound() : Results.Ok(shipment);
        }

        private static async Task<IResult> GetStatus(AppDbContext db, string trackingNo)
        {
            var result = await db.Shipments
                .Where(s => s.TrackingNo == trackingNo)
                .Select(s => new { s.TrackingNo, s.Status, s.CreatedAt })
                .FirstOrDefaultAsync();

            return result is null ? Results.NotFound() : Results.Ok(result);
        }

        private static async Task<IResult> UpdateStatus(
            int id, ShipmentStatus status, AppDbContext db, OutboxSignal signal)
        {
            var shipment = await db.Shipments.FindAsync(id);
            if (shipment is null) return Results.NotFound();

            shipment.Status = status;
            db.SearchOutbox.Add(new SearchOutbox { ShipmentId = id });
            await db.SaveChangesAsync();

            signal.Notify();

            return Results.Ok("Gönderi güncellendi");
        }
    }
}
