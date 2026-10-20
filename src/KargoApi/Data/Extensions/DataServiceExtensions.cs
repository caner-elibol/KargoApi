using Microsoft.EntityFrameworkCore;

namespace KargoApi.Data.Extensions;

public static class DataServiceExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {


        services.AddDbContext<AppDbContext>(o =>
        {
            o.UseSqlServer(
                configuration.GetConnectionString("Default"),
                sql => sql.EnableRetryOnFailure());

            if (environment.IsDevelopment())
                o.EnableSensitiveDataLogging();
        });

        return services;
    }
}