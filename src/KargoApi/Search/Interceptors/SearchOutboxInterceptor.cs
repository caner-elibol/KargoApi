using KargoApi.Data;
using KargoApi.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KargoApi.Search;

public class SearchOutboxInterceptor(OutboxSignal signal, ILogger<SearchOutboxInterceptor> logger) : SaveChangesInterceptor
{
    private bool _hasNewOutboxEntries;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddOutboxEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddOutboxEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        NotifyIfNeeded();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        NotifyIfNeeded();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void AddOutboxEntries(DbContext? context)
    {
        if (context is not AppDbContext db) return;

        var aggregates = db.ChangeTracker
            .Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                if (domainEvent is ShipmentStatusChanged changed)
                {
                    db.SearchOutbox.Add(new SearchOutbox { ShipmentId = changed.ShipmentId });
                    _hasNewOutboxEntries = true;
                }
            }

            aggregate.ClearDomainEvents();
        }
    }

    private void NotifyIfNeeded()
    {
        if (!_hasNewOutboxEntries) return;

        _hasNewOutboxEntries = false;
        signal.Notify();
    }
}