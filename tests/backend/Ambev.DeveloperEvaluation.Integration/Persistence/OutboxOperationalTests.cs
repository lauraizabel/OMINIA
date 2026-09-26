using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.HealthChecks;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class OutboxOperationalTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 26, 1, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlFixture _database;

    public OutboxOperationalTests(PostgreSqlFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Health_is_degraded_for_an_old_backlog_or_dead_letters()
    {
        await ResetDatabaseAsync();
        var clock = new MutableTimeProvider(Now);
        var options = Options.Create(new OutboxOptions { DegradedBacklogAgeSeconds = 60 });

        await using (var emptyContext = CreateContext())
        {
            var health = new OutboxHealthCheck(emptyContext, clock, options);
            Assert.Equal(HealthStatus.Healthy, (await health.CheckHealthAsync(new HealthCheckContext())).Status);
        }

        await using (var context = CreateContext())
        {
            context.OutboxMessages.Add(Message(Now));
            await context.SaveChangesAsync();
            var health = new OutboxHealthCheck(context, clock, options);
            Assert.Equal(HealthStatus.Healthy, (await health.CheckHealthAsync(new HealthCheckContext())).Status);
            var administration = new OutboxAdministration(context, clock);
            Assert.False(await administration.ReplayAsync(Guid.NewGuid()));
            Assert.False(await administration.ReplayAsync(
                await context.OutboxMessages.Select(message => message.Id).SingleAsync()));
        }

        clock.Advance(TimeSpan.FromMinutes(2));
        await using (var oldContext = CreateContext())
        {
            var health = new OutboxHealthCheck(oldContext, clock, options);
            Assert.Equal(HealthStatus.Degraded, (await health.CheckHealthAsync(new HealthCheckContext())).Status);
        }

        await using (var deadLetterContext = CreateContext())
        {
            var message = await deadLetterContext.OutboxMessages.SingleAsync();
            var owner = Guid.NewGuid();
            message.Claim(owner, clock.GetUtcNow().AddMinutes(1));
            message.DeadLetter(owner, clock.GetUtcNow(), "failed");
            await deadLetterContext.SaveChangesAsync();
            var health = new OutboxHealthCheck(deadLetterContext, clock, options);
            Assert.Equal(HealthStatus.Degraded, (await health.CheckHealthAsync(new HealthCheckContext())).Status);
        }
    }

    [Fact]
    public async Task Cleanup_removes_only_processed_events_older_than_retention()
    {
        await ResetDatabaseAsync();
        var clock = new MutableTimeProvider(Now);
        await using var context = CreateContext();
        var expired = Message(Now.AddDays(-10));
        var recent = Message(Now.AddDays(-1));
        var pending = Message(Now.AddDays(-10));
        Complete(expired, Now.AddDays(-9));
        Complete(recent, Now.AddHours(-12));
        context.OutboxMessages.AddRange(expired, recent, pending);
        await context.SaveChangesAsync();
        var processor = new OutboxProcessor(
            context,
            new NullPublisher(),
            clock,
            Options.Create(new OutboxOptions { ProcessedRetentionDays = 7 }),
            NullLogger<OutboxProcessor>.Instance);

        var removed = await processor.CleanupProcessedAsync();

        Assert.Equal(1, removed);
        Assert.Equal(2, await context.OutboxMessages.CountAsync());
        Assert.True(await context.OutboxMessages.AnyAsync(message => message.Id == recent.Id));
        Assert.True(await context.OutboxMessages.AnyAsync(message => message.Id == pending.Id));
    }

    [Fact]
    public async Task Worker_cycle_processes_available_events_and_respects_the_cleanup_interval()
    {
        await ResetDatabaseAsync();
        var clock = new MutableTimeProvider(Now);
        await using (var seedContext = CreateContext())
        {
            var expired = Message(Now.AddDays(-10));
            Complete(expired, Now.AddDays(-9));
            seedContext.OutboxMessages.AddRange(Message(Now), expired);
            await seedContext.SaveChangesAsync();
        }

        var publisher = new RecordingPublisher();
        var settings = Options.Create(new OutboxOptions
        {
            BatchSize = 10,
            PollIntervalSeconds = 1,
            CleanupIntervalMinutes = 60,
            ProcessedRetentionDays = 7
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IOptions<OutboxOptions>>(settings);
        services.AddSingleton<ISaleDomainEventPublisher>(publisher);
        services.AddDbContext<DefaultContext>(options => options.UseNpgsql(
            _database.ConnectionString,
            provider => provider.MigrationsAssembly(typeof(DefaultContext).Assembly.FullName)));
        services.AddScoped<OutboxProcessor>();
        await using var provider = services.BuildServiceProvider();
        var worker = new OutboxWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            settings,
            clock,
            NullLogger<OutboxWorker>.Instance);

        Assert.Equal(1, await worker.RunCycleAsync());
        Assert.Equal(0, await worker.RunCycleAsync());
        Assert.Single(publisher.Published);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(1200);
        await worker.StopAsync(CancellationToken.None);
    }

    private static void Complete(OutboxMessage message, DateTimeOffset processedAt)
    {
        var owner = Guid.NewGuid();
        message.Claim(owner, processedAt.AddMinutes(1));
        message.Complete(owner, processedAt);
    }

    private static OutboxMessage Message(DateTimeOffset createdAt) => OutboxMessage.From(
        new SaleCreatedEvent(Guid.NewGuid(), 1, createdAt),
        0,
        "operations-test",
        createdAt);

    private DefaultContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseNpgsql(
                _database.ConnectionString,
                provider => provider.MigrationsAssembly(typeof(DefaultContext).Assembly.FullName))
            .Options;
        return new DefaultContext(options);
    }

    private async Task ResetDatabaseAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    private sealed class NullPublisher : ISaleDomainEventPublisher
    {
        public Task PublishAsync(SaleEventMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingPublisher : ISaleDomainEventPublisher
    {
        public List<SaleEventMessage> Published { get; } = [];

        public Task PublishAsync(SaleEventMessage message, CancellationToken cancellationToken = default)
        {
            Published.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
