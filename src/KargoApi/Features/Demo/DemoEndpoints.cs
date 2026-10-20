using KargoApi.Data;
using KargoApi.Entities;
using KargoApi.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace KargoApi.Features.Demo;

public static class DemoEndpoints
{
    public static IEndpointRouteBuilder MapDemoEndpoints(this IEndpointRouteBuilder app)
    {
        var demo = app.MapGroup("/demo").WithTags("Demo");
        demo.MapPost("/transaction", Transaction);
        demo.MapPost("/concurrency", Concurrency);

        app.MapGet("/stats/status/{status}", StatsByStatus).WithTags("Demo");

        return app;
    }

    private static async Task<IResult> Transaction(AppDbContext db, int originBranchId)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();

            await using var tx = await db.Database.BeginTransactionAsync();

            var branch = await db.Branches.FirstAsync(b => b.Id == 1);
            branch.Name = "Kadıköy Şube - TEST";
            await db.SaveChangesAsync();

            db.Shipments.Add(new Shipment
            {
                TrackingNo = "EF001",
                SenderName = "Test",
                ReceiverCity = "Ankara",
                OriginBranchId = originBranchId,
                Status = ShipmentStatus.Accepted,
                WeightKg = 1m,
                CreatedAt = DateTime.Now
            });
            await db.SaveChangesAsync();

            await tx.CommitAsync();
            return Results.Ok("Tamamlandı");
        });
    }

    private static async Task<IResult> Concurrency(AppDbContext db)
    {
        var branch = await db.Branches.FirstAsync(b => b.Id == 1);

        await db.Database.ExecuteSqlAsync(
            $"UPDATE dbo.Branches SET Name = N'Ayşe değiştirdi' WHERE Id = 1");

        branch.Name = "Ali değiştirdi";

        try
        {
            await db.SaveChangesAsync();
            return Results.Ok("Kaydedildi");
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict("Bu kayıt siz okuduktan sonra başkası tarafından değiştirildi.");
        }
    }

    private static async Task<IResult> StatsByStatus(AppDbContext db, byte status)
    {
        var stats = await db.Database
            .SqlQuery<StatusStats>($"EXEC dbo.GetShipmentStatsByStatus @Status = {status}")
            .ToListAsync();

        return Results.Ok(stats.FirstOrDefault());
    }
}

public class StatusStats
{
    public int Adet { get; set; }
    public decimal? ToplamKg { get; set; }
}