namespace Ambev.DeveloperEvaluation.Application.Observability;

public interface IOutboxAdministration
{
    Task<OutboxStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<bool> ReplayAsync(Guid eventId, CancellationToken cancellationToken = default);
}

public sealed class OutboxStatus
{
    public OutboxStatus(
        int pendingCount,
        int deadLetterCount,
        DateTimeOffset? oldestPendingAt,
        IReadOnlyList<DeadLetterEvent> deadLetters)
    {
        PendingCount = pendingCount;
        DeadLetterCount = deadLetterCount;
        OldestPendingAt = oldestPendingAt;
        DeadLetters = deadLetters;
    }

    public int PendingCount { get; }
    public int DeadLetterCount { get; }
    public DateTimeOffset? OldestPendingAt { get; }
    public IReadOnlyList<DeadLetterEvent> DeadLetters { get; }
}

public sealed class DeadLetterEvent
{
    public DeadLetterEvent(
        Guid eventId,
        string eventType,
        Guid saleId,
        long version,
        int attemptCount,
        DateTimeOffset failedAt,
        string? lastError)
    {
        EventId = eventId;
        EventType = eventType;
        SaleId = saleId;
        Version = version;
        AttemptCount = attemptCount;
        FailedAt = failedAt;
        LastError = lastError;
    }

    public Guid EventId { get; }
    public string EventType { get; }
    public Guid SaleId { get; }
    public long Version { get; }
    public int AttemptCount { get; }
    public DateTimeOffset FailedAt { get; }
    public string? LastError { get; }
}
