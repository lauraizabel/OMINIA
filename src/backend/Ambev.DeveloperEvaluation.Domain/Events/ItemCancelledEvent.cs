namespace Ambev.DeveloperEvaluation.Domain.Events;

public sealed record ItemCancelledEvent(
    Guid SaleId,
    Guid ItemId,
    long Version,
    DateTimeOffset OccurredAt)
    : SaleDomainEvent(SaleId, Version, OccurredAt);
