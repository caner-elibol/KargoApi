using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KargoApi.Search.Extensions;

public static class SearchServiceExtensions
{
    public static IServiceCollection AddSearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(_ =>
            new ElasticsearchClient(
                new ElasticsearchClientSettings(
                    new Uri(configuration["Elasticsearch:Url"]!))));

        services.AddScoped<ISaveChangesInterceptor, SearchOutboxInterceptor>();
        services.AddScoped<ShipmentIndexer>();
        services.AddSingleton<OutboxSignal>();
        services.AddHostedService<SearchOutboxWorker>();

        return services;
    }
}