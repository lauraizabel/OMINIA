using Ambev.DeveloperEvaluation.Domain.Events;

namespace Ambev.DeveloperEvaluation.Application.Observability;

public interface ISaleDomainEventPublisher
{
    Task PublishAsync(
        SaleDomainEvent domainEvent,
        string correlationId,
        CancellationToken cancellationToken = default);
}
