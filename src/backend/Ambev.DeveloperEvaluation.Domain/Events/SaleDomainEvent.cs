namespace Ambev.DeveloperEvaluation.Domain.Events;

public abstract record SaleDomainEvent(
    Guid SaleId,
    long Version,
    DateTimeOffset OccurredAt);
