using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.ORM.Observability;

public sealed class StructuredLogSaleDomainEventPublisher : ISaleDomainEventPublisher
{
    private readonly ILogger<StructuredLogSaleDomainEventPublisher> _logger;

    public StructuredLogSaleDomainEventPublisher(ILogger<StructuredLogSaleDomainEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(
        SaleDomainEvent domainEvent,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sale domain event published. EventId: {EventId}, EventType: {EventType}, SaleId: {SaleId}, Version: {Version}, OccurredAt: {OccurredAt}, CorrelationId: {CorrelationId}",
            domainEvent.EventId,
            domainEvent.GetType().Name,
            domainEvent.SaleId,
            domainEvent.Version,
            domainEvent.OccurredAt,
            correlationId);

        return Task.CompletedTask;
    }
}
