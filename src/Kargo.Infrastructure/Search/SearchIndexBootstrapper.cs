using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Kargo.Infrastructure.Search
{
    public static class SearchIndexBootstrapper
    {
        private const string IndexName = "shipments-v3";
        private const string AliasName = "shipments";

        public static async Task EnsureAsync(IServiceProvider sp, IConfiguration config, ILogger logger)
        {
            var es = sp.GetRequiredService<ElasticsearchClient>();

            var exists = await es.Indices.ExistsAsync(IndexName);
            if (exists.Exists)
            {
                logger.LogInformation("{Index} zaten var, Elasticsearch bootstrap atlanıyor", IndexName);
                return;
            }

            using var http = new HttpClient { BaseAddress = new Uri(config["Elasticsearch:Url"]!) };

            // 1. Index'i mapping ile oluştur
            var mappingPath = Path.Combine(AppContext.BaseDirectory, "Search", "shipments-v3.json");
            var mapping = await File.ReadAllTextAsync(mappingPath);

            var create = await http.PutAsync(IndexName,
                new StringContent(mapping, Encoding.UTF8, "application/json"));
            create.EnsureSuccessStatusCode();
            logger.LogInformation("{Index} oluşturuldu", IndexName);

            // 2. Verileri SQL Server'dan aktar
            var indexer = sp.GetRequiredService<ShipmentIndexer>();
            var total = await indexer.IndexAllAsync(IndexName);

            // 3. Alias'ı oluştur
            var aliasBody = $$"""
            { "actions": [ { "add": { "index": "{{IndexName}}", "alias": "{{AliasName}}" } } ] }
            """;

            var alias = await http.PostAsync("_aliases",
                new StringContent(aliasBody, Encoding.UTF8, "application/json"));
            alias.EnsureSuccessStatusCode();

            logger.LogInformation("Elasticsearch bootstrap tamamlandı: {Total} gönderi, alias {Alias}",
                total, AliasName);
        }
    }
}
