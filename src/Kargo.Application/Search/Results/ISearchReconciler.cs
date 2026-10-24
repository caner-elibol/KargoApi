namespace Kargo.Application.Search;

public record ReconciliationReport(
    int SqlCount,
    int EsCount,
    int MissingCount,
    int OrphanCount,
    IReadOnlyList<Guid> MissingSample,
    IReadOnlyList<Guid> OrphanSample,
    bool DryRun,
    bool LimitExceeded,
    int Enqueued);

public interface ISearchReconciler
{
    /// <summary>
    /// SQL ile Elasticsearch'teki gönderi Id kümelerini karşılaştırır.
    /// dryRun=true ise sadece rapor üretir, hiçbir şey yazmaz.
    /// Fark varsa outbox'a satır ekler; Elasticsearch'e worker yazar.
    /// </summary>
    Task<ReconciliationReport> ReconcileAsync(
        bool dryRun = true,
        int maxFixes = 50_000,
        CancellationToken ct = default);
}