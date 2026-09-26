using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.ORM.Outbox;

namespace Ambev.DeveloperEvaluation.ORM;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DefaultContext _context;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly TimeProvider _timeProvider;

    public UnitOfWork(
        DefaultContext context,
        ICorrelationIdProvider correlationIdProvider,
        TimeProvider timeProvider)
    {
        _context = context;
        _correlationIdProvider = correlationIdProvider;
        _timeProvider = timeProvider;
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        var pendingEvents = CapturePendingEvents();
        Enqueue(pendingEvents);
        var affectedRows = await _context.SaveChangesAsync(cancellationToken);

        ClearCommittedEvents(pendingEvents);

        return affectedRows;
    }

    private void Enqueue(IEnumerable<PendingSaleEvents> pendingEvents)
    {
        var trackedEventIds = _context.OutboxMessages.Local
            .Select(message => message.Id)
            .ToHashSet();
        var correlationId = _correlationIdProvider.CorrelationId;
        var createdAt = _timeProvider.GetUtcNow();

        var messages = pendingEvents
            .SelectMany(pending => pending.Events.Select((domainEvent, sequence) => (domainEvent, sequence)))
            .Where(item => trackedEventIds.Add(item.domainEvent.EventId))
            .Select(item => OutboxMessage.From(
                item.domainEvent,
                item.sequence,
                correlationId,
                createdAt));

        _context.OutboxMessages.AddRange(messages);
    }

    private PendingSaleEvents[] CapturePendingEvents()
    {
        return _context.ChangeTracker
            .Entries<Sale>()
            .Select(entry => entry.Entity)
            .Where(sale => sale.DomainEvents.Count > 0)
            .Select(sale => new PendingSaleEvents(sale, sale.DomainEvents.ToArray()))
            .ToArray();
    }

    private static void ClearCommittedEvents(IEnumerable<PendingSaleEvents> pendingEvents)
    {
        foreach (var pending in pendingEvents)
            pending.Sale.ClearDomainEvents();
    }

    private sealed record PendingSaleEvents(Sale Sale, IReadOnlyList<SaleDomainEvent> Events);
}
