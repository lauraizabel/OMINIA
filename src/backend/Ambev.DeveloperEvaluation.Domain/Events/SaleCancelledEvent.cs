namespace Ambev.DeveloperEvaluation.Domain.Events;

public sealed record SaleCancelledEvent(
    Guid SaleId,
    long Version,
    DateTimeOffset OccurredAt)
    : SaleDomainEvent(SaleId, Version, OccurredAt);
