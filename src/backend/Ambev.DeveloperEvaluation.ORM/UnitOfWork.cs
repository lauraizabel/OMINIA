using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.ORM;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DefaultContext _context;
    private readonly ISaleDomainEventPublisher _eventPublisher;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<UnitOfWork> _logger;

    public UnitOfWork(
        DefaultContext context,
        ISaleDomainEventPublisher eventPublisher,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<UnitOfWork> logger)
    {
        _context = context;
        _eventPublisher = eventPublisher;
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        var pendingEvents = CapturePendingEvents();
        var affectedRows = await _context.SaveChangesAsync(cancellationToken);

        ClearCommittedEvents(pendingEvents);
        await PublishBestEffortAsync(pendingEvents, _correlationIdProvider.CorrelationId);

        return affectedRows;
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

    private async Task PublishBestEffortAsync(
        IEnumerable<PendingSaleEvents> pendingEvents,
        string correlationId)
    {
        foreach (var domainEvent in pendingEvents.SelectMany(pending => pending.Events))
        {
            try
            {
                // Persistence has already committed. Publication must not make a successful
                // write look rolled back to the caller, including when the request is cancelled.
                await _eventPublisher.PublishAsync(domainEvent, correlationId, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Sale domain event publication failed after commit. EventId: {EventId}, EventType: {EventType}, SaleId: {SaleId}, Version: {Version}, CorrelationId: {CorrelationId}",
                    domainEvent.EventId,
                    domainEvent.GetType().Name,
                    domainEvent.SaleId,
                    domainEvent.Version,
                    correlationId);
            }
        }
    }

    private sealed record PendingSaleEvents(Sale Sale, IReadOnlyList<SaleDomainEvent> Events);
}
