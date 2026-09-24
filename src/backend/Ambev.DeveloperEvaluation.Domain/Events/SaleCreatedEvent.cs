namespace Ambev.DeveloperEvaluation.Domain.Events;

public sealed record SaleCreatedEvent(
    Guid SaleId,
    long Version,
    DateTimeOffset OccurredAt)
    : SaleDomainEvent(SaleId, Version, OccurredAt);
