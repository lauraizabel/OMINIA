using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public sealed class OutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxWorker> _logger;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _nextCleanupAt = DateTimeOffset.MinValue;

    public OutboxWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        TimeProvider timeProvider,
        ILogger<OutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));
        do
        {
            try
            {
                var processed = await RunCycleAsync(stoppingToken);
                if (processed == _options.BatchSize)
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox processing cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> RunCycleAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
        var processed = await processor.ProcessBatchAsync(cancellationToken);
        if (_timeProvider.GetUtcNow() < _nextCleanupAt)
            return processed;

        var removed = await processor.CleanupProcessedAsync(cancellationToken);
        if (removed > 0)
            _logger.LogInformation("Removed {Count} processed outbox events after retention.", removed);
        _nextCleanupAt = _timeProvider.GetUtcNow().AddMinutes(_options.CleanupIntervalMinutes);
        return processed;
    }
}
