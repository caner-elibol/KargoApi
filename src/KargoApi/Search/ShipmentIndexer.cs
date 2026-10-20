using Elastic.Clients.Elasticsearch;
using KargoApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KargoApi.Search
{
    public class ShipmentIndexer(AppDbContext db, ElasticsearchClient es, ILogger<ShipmentIndexer> logger)
    {
        public async Task<int> IndexAllAsync(string indexName, CancellationToken ct = default)
        {
            const int batchSize = 5000;
            var lastId = 0;
            var total = 0;

            while (true)
            {
                var batch = await db.Shipments
                    .AsNoTracking()
                    .Where(s => s.Id > lastId)
                    .OrderBy(s => s.Id)
                    .Take(batchSize)
                    .ToListAsync(ct);

                if (batch.Count == 0) break;

                var docs = batch.Select(ShipmentDocument.From).ToList();

                var response = await es.BulkAsync(b => b
                    .Index(indexName)
                    .IndexMany(docs, (op, doc) => op.Id(doc.Id.ToString())), ct);

                if (!response.IsValidResponse || response.Errors)
                {
                    var reason = response.ItemsWithErrors.FirstOrDefault()?.Error?.Reason
                                 ?? response.DebugInformation;
                    throw new InvalidOperationException($"Elasticsearch aktarımı başarısız: {reason}");
                }

                total += docs.Count;
                lastId = batch[^1].Id;
                logger.LogInformation("{Total} gönderi aktarıldı", total);
            }

            return total;
        }
    }
}
