using Ambev.DeveloperEvaluation.ORM.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Ambev.DeveloperEvaluation.ORM.HealthChecks;

public sealed class OutboxHealthCheck : IHealthCheck
{
    private readonly DefaultContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly OutboxOptions _options;

    public OutboxHealthCheck(
        DefaultContext context,
        TimeProvider timeProvider,
        IOptions<OutboxOptions> options)
    {
        _context = context;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var deadLetterCount = await _context.OutboxMessages
            .CountAsync(message => message.FailedAt != null, cancellationToken);
        var pending = _context.OutboxMessages
            .Where(message => message.ProcessedAt == null && message.FailedAt == null);
        var pendingCount = await pending.CountAsync(cancellationToken);
        var oldestPendingAt = await pending
            .MinAsync(message => (DateTimeOffset?)message.CreatedAt, cancellationToken);

        var data = new Dictionary<string, object>
        {
            ["pending"] = pendingCount,
            ["deadLetter"] = deadLetterCount
        };

        if (deadLetterCount > 0)
            return HealthCheckResult.Degraded("The outbox contains dead-letter events.", data: data);

        if (oldestPendingAt is null)
            return HealthCheckResult.Healthy("The outbox backlog is within its operating threshold.", data);

        if (_timeProvider.GetUtcNow() - oldestPendingAt.Value >
            TimeSpan.FromSeconds(_options.DegradedBacklogAgeSeconds))
        {
            return HealthCheckResult.Degraded("The outbox backlog is older than expected.", data: data);
        }

        return HealthCheckResult.Healthy("The outbox backlog is within its operating threshold.", data);
    }
}
