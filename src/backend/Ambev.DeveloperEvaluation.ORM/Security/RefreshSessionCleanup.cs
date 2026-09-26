using Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ambev.DeveloperEvaluation.ORM.Security;

public sealed class RefreshSessionCleanup
{
    private readonly DefaultContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly RefreshSessionOptions _options;

    public RefreshSessionCleanup(
        DefaultContext context,
        TimeProvider timeProvider,
        IOptions<RefreshSessionOptions> options)
    {
        _context = context;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<int> DeleteBatchAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = _timeProvider.GetUtcNow().AddDays(-_options.CleanupRetentionDays);
        var ids = await _context.RefreshSessions
            .Where(session => session.AbsoluteExpiresAt < cutoff)
            .OrderBy(session => session.AbsoluteExpiresAt)
            .ThenBy(session => session.Id)
            .Select(session => session.Id)
            .Take(_options.CleanupBatchSize)
            .ToArrayAsync(cancellationToken);

        if (ids.Length == 0)
            return 0;

        return await _context.RefreshSessions
            .Where(session => ids.Contains(session.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
