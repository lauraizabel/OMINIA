using System.Text.Json;
using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.Domain.Events;

namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }
    public Guid AggregateId { get; private set; }
    public long AggregateVersion { get; private set; }
    public int Sequence { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public Guid? LockToken { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    public static OutboxMessage From(
        SaleDomainEvent domainEvent,
        int sequence,
        string correlationId,
        DateTimeOffset createdAt)
    {
        return new OutboxMessage
        {
            Id = domainEvent.EventId,
            AggregateId = domainEvent.SaleId,
            AggregateVersion = domainEvent.Version,
            Sequence = sequence,
            EventType = domainEvent.GetType().Name,
            Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            CorrelationId = correlationId,
            OccurredAt = domainEvent.OccurredAt,
            CreatedAt = createdAt,
            NextAttemptAt = createdAt
        };
    }

    public SaleEventMessage ToEventMessage() => new(
        Id,
        EventType,
        AggregateId,
        AggregateVersion,
        OccurredAt,
        CorrelationId,
        Payload);

    public void Claim(Guid lockToken, DateTimeOffset lockedUntil)
    {
        LockToken = lockToken;
        LockedUntil = lockedUntil;
        AttemptCount++;
    }

    public void Complete(Guid lockToken, DateTimeOffset processedAt)
    {
        EnsureLockOwner(lockToken);
        ProcessedAt = processedAt;
        LockToken = null;
        LockedUntil = null;
        LastError = null;
    }

    public void Retry(Guid lockToken, DateTimeOffset nextAttemptAt, string error)
    {
        EnsureLockOwner(lockToken);
        NextAttemptAt = nextAttemptAt;
        LastError = error;
        LockToken = null;
        LockedUntil = null;
    }

    public void DeadLetter(Guid lockToken, DateTimeOffset failedAt, string error)
    {
        EnsureLockOwner(lockToken);
        FailedAt = failedAt;
        LastError = error;
        LockToken = null;
        LockedUntil = null;
    }

    public void Replay(DateTimeOffset nextAttemptAt)
    {
        if (FailedAt is null)
            throw new InvalidOperationException("Only dead-letter events can be replayed.");

        FailedAt = null;
        LastError = null;
        AttemptCount = 0;
        NextAttemptAt = nextAttemptAt;
        LockToken = null;
        LockedUntil = null;
    }

    private void EnsureLockOwner(Guid lockToken)
    {
        if (LockToken != lockToken)
            throw new InvalidOperationException("The outbox message lease is no longer owned by this worker.");
    }
}
