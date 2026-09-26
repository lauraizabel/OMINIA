using Ambev.DeveloperEvaluation.Application.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public sealed class OutboxProcessor
{
    private readonly DefaultContext _context;
    private readonly ISaleDomainEventPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        DefaultContext context,
        ISaleDomainEventPublisher publisher,
        TimeProvider timeProvider,
        IOptions<OutboxOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _context = context;
        _publisher = publisher;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        var lockToken = Guid.NewGuid();
        var messages = await ClaimBatchAsync(lockToken, cancellationToken);

        foreach (var message in messages)
            await ProcessMessageAsync(message, lockToken, cancellationToken);

        return messages.Count;
    }

    public Task<int> CleanupProcessedAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = _timeProvider.GetUtcNow().AddDays(-_options.ProcessedRetentionDays);
        return _context.OutboxMessages
            .Where(message => message.ProcessedAt != null && message.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<List<OutboxMessage>> ClaimBatchAsync(
        Guid lockToken,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var lockedUntil = now.AddSeconds(_options.LeaseSeconds);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var messages = await _context.OutboxMessages
            .FromSqlInterpolated($$"""
                SELECT candidate.*
                FROM "OutboxMessages" AS candidate
                WHERE candidate."ProcessedAt" IS NULL
                  AND candidate."FailedAt" IS NULL
                  AND candidate."NextAttemptAt" <= {{now}}
                  AND (candidate."LockedUntil" IS NULL OR candidate."LockedUntil" < {{now}})
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "OutboxMessages" AS earlier
                      WHERE earlier."AggregateId" = candidate."AggregateId"
                        AND earlier."ProcessedAt" IS NULL
                        AND (earlier."AggregateVersion", earlier."Sequence")
                            < (candidate."AggregateVersion", candidate."Sequence")
                  )
                ORDER BY candidate."OccurredAt", candidate."Id"
                FOR UPDATE SKIP LOCKED
                LIMIT {{_options.BatchSize}}
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
            message.Claim(lockToken, lockedUntil);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages;
    }

    private async Task ProcessMessageAsync(
        OutboxMessage message,
        Guid lockToken,
        CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishAsync(message.ToEventMessage(), cancellationToken);
            message.Complete(lockToken, _timeProvider.GetUtcNow());
            if (!await SaveStateIfLeaseOwnedAsync(message, cancellationToken))
                return;

            _logger.LogInformation(
                "Outbox event delivered. EventId: {EventId}, EventType: {EventType}, SaleId: {SaleId}, Version: {Version}, Attempt: {Attempt}",
                message.Id,
                message.EventType,
                message.AggregateId,
                message.AggregateVersion,
                message.AttemptCount);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var now = _timeProvider.GetUtcNow();
            var error = Truncate(exception.Message, 2000);

            if (message.AttemptCount >= _options.MaxAttempts)
            {
                message.DeadLetter(lockToken, now, error);
                _logger.LogError(
                    exception,
                    "Outbox event moved to dead letter. EventId: {EventId}, EventType: {EventType}, SaleId: {SaleId}, Attempts: {Attempts}",
                    message.Id,
                    message.EventType,
                    message.AggregateId,
                    message.AttemptCount);
            }
            else
            {
                var retryAt = now.Add(Backoff(message.AttemptCount));
                message.Retry(lockToken, retryAt, error);
                _logger.LogWarning(
                    exception,
                    "Outbox event delivery failed. EventId: {EventId}, Attempt: {Attempt}, RetryAt: {RetryAt}",
                    message.Id,
                    message.AttemptCount,
                    retryAt);
            }

            await SaveStateIfLeaseOwnedAsync(message, cancellationToken);
        }
    }

    private async Task<bool> SaveStateIfLeaseOwnedAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _context.Entry(message).State = EntityState.Detached;
            _logger.LogWarning(
                exception,
                "Outbox event lease was lost before acknowledgement. EventId: {EventId}",
                message.Id);
            return false;
        }
    }

    private TimeSpan Backoff(int attempt)
    {
        var multiplier = Math.Pow(2, Math.Max(0, attempt - 1));
        var seconds = Math.Min(_options.MaxRetrySeconds, _options.BaseRetrySeconds * multiplier);
        return TimeSpan.FromSeconds(seconds);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
