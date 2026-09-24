namespace Ambev.DeveloperEvaluation.Domain.Events;

public abstract record SaleDomainEvent(
    Guid SaleId,
    long Version,
    DateTimeOffset OccurredAt)
{
    public Guid EventId { get; } = Guid.NewGuid();
}
