using Ambev.DeveloperEvaluation.Application.Observability;
using Microsoft.EntityFrameworkCore;

namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public sealed class OutboxAdministration : IOutboxAdministration
{
    private const int DeadLetterPageSize = 20;
    private readonly DefaultContext _context;
    private readonly TimeProvider _timeProvider;

    public OutboxAdministration(DefaultContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<OutboxStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var pending = _context.OutboxMessages
            .AsNoTracking()
            .Where(message => message.ProcessedAt == null && message.FailedAt == null);
        var pendingCount = await pending.CountAsync(cancellationToken);
        var oldestPendingAt = await pending
            .MinAsync(message => (DateTimeOffset?)message.CreatedAt, cancellationToken);
        var deadLetters = await _context.OutboxMessages
            .AsNoTracking()
            .Where(message => message.FailedAt != null)
            .OrderByDescending(message => message.FailedAt)
            .Take(DeadLetterPageSize)
            .Select(message => new DeadLetterEvent(
                message.Id,
                message.EventType,
                message.AggregateId,
                message.AggregateVersion,
                message.AttemptCount,
                message.FailedAt!.Value,
                message.LastError))
            .ToListAsync(cancellationToken);
        var deadLetterCount = await _context.OutboxMessages
            .CountAsync(message => message.FailedAt != null, cancellationToken);

        return new OutboxStatus(pendingCount, deadLetterCount, oldestPendingAt, deadLetters);
    }

    public async Task<bool> ReplayAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages
            .SingleOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);
        if (message?.FailedAt is null)
            return false;

        message.Replay(_timeProvider.GetUtcNow());
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
