namespace Ambev.DeveloperEvaluation.Domain.Events;

public sealed record SaleModifiedEvent(
    Guid SaleId,
    long Version,
    DateTimeOffset OccurredAt)
    : SaleDomainEvent(SaleId, Version, OccurredAt);
