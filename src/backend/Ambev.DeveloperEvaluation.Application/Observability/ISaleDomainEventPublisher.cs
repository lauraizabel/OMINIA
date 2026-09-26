namespace Ambev.DeveloperEvaluation.Application.Observability;

public interface ISaleDomainEventPublisher
{
    Task PublishAsync(
        SaleEventMessage message,
        CancellationToken cancellationToken = default);
}

public sealed class SaleEventMessage
{
    public SaleEventMessage(
        Guid eventId,
        string eventType,
        Guid saleId,
        long version,
        DateTimeOffset occurredAt,
        string correlationId,
        string payload)
    {
        EventId = eventId;
        EventType = eventType;
        SaleId = saleId;
        Version = version;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
        Payload = payload;
    }

    public Guid EventId { get; }
    public string EventType { get; }
    public Guid SaleId { get; }
    public long Version { get; }
    public DateTimeOffset OccurredAt { get; }
    public string CorrelationId { get; }
    public string Payload { get; }
}
