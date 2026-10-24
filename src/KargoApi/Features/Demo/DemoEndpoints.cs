using Kargo.Infrastructure.Data;
using Kargo.Domain;
using Kargo.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text;

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
            db.Shipments.Add(Shipment.Create(
    trackingNo: new TrackingNumber(RegexGenerator.GenerateValidString()),
    senderName: "Test",
    receiverCity: "Ankara",
    originBranchId: originBranchId,
    weightKg: 1m,
    createdAt: DateTime.Now));
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

public class RegexGenerator
{
    private static readonly Random _random = new();

    public static string GenerateValidString()
    {
        StringBuilder builder = new();

        // 1. İlk 2 karakter için rastgele büyük harf (A-Z) üret
        for (int i = 0; i < 2; i++)
        {
            char letter = (char)_random.Next('A', 'Z' + 1);
            builder.Append(letter);
        }

        // 2. 3 ile 18 arasında rastgele bir uzunluk belirle (üst sınır dahil olması için 19)
        int digitLength = _random.Next(3, 19);

        // 3. Belirlenen uzunlukta rastgele rakam (0-9) üret
        for (int i = 0; i < digitLength; i++)
        {
            int digit = _random.Next(0, 10);
            builder.Append(digit);
        }

        return builder.ToString();
    }
}