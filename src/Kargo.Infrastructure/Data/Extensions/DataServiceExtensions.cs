using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kargo.Infrastructure.Data.Extensions;

public static class DataServiceExtensions
{
    public static IServiceCollection AddPersistence(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
    {
        services.AddDbContext<AppDbContext>((sp, o) =>
        {
            o.UseSqlServer(
                configuration.GetConnectionString("Default"),
                sql => sql.EnableRetryOnFailure());

            o.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());

            if (environment.IsDevelopment())
                o.EnableSensitiveDataLogging();
        });

        return services;
    }
}