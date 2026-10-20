using KargoApi.Data;
using KargoApi.Features.Demo;
using KargoApi.Search;
using Microsoft.EntityFrameworkCore;

namespace KargoApi;

public static class DevelopmentInitializer
{
    public static async Task InitializeDevelopmentAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILogger<Program>>();

        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await DevDataSeeder.SeedAsync(db, logger);
        await SearchIndexBootstrapper.EnsureAsync(sp, app.Configuration, logger);

        app.MapDemoEndpoints();
    }
}