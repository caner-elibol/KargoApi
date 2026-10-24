using Elastic.Clients.Elasticsearch;
using Kargo.Application.Search;
using Kargo.Infrastructure.Search.Interceptors;
using Kargo.Infrastructure.Search.Workers;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kargo.Infrastructure.Search.Extensions;

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

        services.AddScoped<IShipmentIndexer, ShipmentIndexer>();
        services.AddScoped<IShipmentSearch, ShipmentSearch>();
        services.AddScoped<ISaveChangesInterceptor, SearchOutboxInterceptor>();
        services.AddScoped<ShipmentIndexer>();


        services.AddSingleton<OutboxSignal>();


        services.AddHostedService<SearchOutboxWorker>();

        return services;
    }
}