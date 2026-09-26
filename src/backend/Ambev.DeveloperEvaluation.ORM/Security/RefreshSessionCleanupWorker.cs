using Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ambev.DeveloperEvaluation.ORM.Security;

public sealed class RefreshSessionCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RefreshSessionOptions _options;
    private readonly ILogger<RefreshSessionCleanupWorker> _logger;

    public RefreshSessionCleanupWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<RefreshSessionOptions> options,
        ILogger<RefreshSessionCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.CleanupIntervalMinutes));
        do
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Refresh-session cleanup cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> RunCycleAsync(CancellationToken cancellationToken = default)
    {
        var totalRemoved = 0;
        for (var batch = 0; batch < _options.CleanupMaxBatchesPerCycle; batch++)
        {
            using var scope = _scopeFactory.CreateScope();
            var cleanup = scope.ServiceProvider.GetRequiredService<RefreshSessionCleanup>();
            var removed = await cleanup.DeleteBatchAsync(cancellationToken);
            totalRemoved += removed;

            if (removed < _options.CleanupBatchSize)
                break;
        }

        if (totalRemoved > 0)
            _logger.LogInformation(
                "Removed {Count} refresh-session records after absolute expiration and retention.",
                totalRemoved);

        return totalRemoved;
    }
}
