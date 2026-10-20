using Elastic.Clients.Elasticsearch;

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

        services.AddScoped<ShipmentIndexer>();
        services.AddSingleton<OutboxSignal>();
        services.AddHostedService<SearchOutboxWorker>();

        return services;
    }
}