using System.Collections.Concurrent;
using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class SaleDomainEventPublishingTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlFixture _database;

    public SaleDomainEventPublishingTests(PostgreSqlFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Commit_persists_sale_and_outbox_event_in_the_same_transaction()
    {
        await ResetDatabaseAsync();
        var clock = new MutableTimeProvider(Now);
        var sale = CreateSale("SALE-OUTBOX", 1);

        await using (var context = CreateContext())
        {
            await new SaleRepository(context).AddAsync(sale);
            await TestUnitOfWork.Create(context, "correlation-123", clock).CommitAsync();
        }

        await using var verificationContext = CreateContext();
        var persistedSale = await verificationContext.Sales.SingleAsync(candidate => candidate.Id == sale.Id);
        var message = await verificationContext.OutboxMessages.SingleAsync();

        Assert.Equal(sale.Id, persistedSale.Id);
        Assert.Empty(sale.DomainEvents);
        Assert.Equal(sale.Id, message.AggregateId);
        Assert.Equal(1, message.AggregateVersion);
        Assert.Equal(0, message.Sequence);
        Assert.Equal("SaleCreatedEvent", message.EventType);
        Assert.Equal("correlation-123", message.CorrelationId);
        Assert.Contains(sale.Id.ToString(), message.Payload);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public async Task Failed_commit_persists_neither_sale_nor_outbox_and_keeps_domain_events()
    {
        await ResetDatabaseAsync();

        await using (var seedContext = CreateContext())
        {
            await new SaleRepository(seedContext).AddAsync(CreateSale("SALE-DUPLICATE", 1));
            await TestUnitOfWork.Create(seedContext).CommitAsync();
        }

        var duplicate = CreateSale("SALE-DUPLICATE", 1);
        await using (var duplicateContext = CreateContext())
        {
            await new SaleRepository(duplicateContext).AddAsync(duplicate);
            await Assert.ThrowsAsync<DbUpdateException>(
                () => TestUnitOfWork.Create(duplicateContext).CommitAsync());
        }

        await using var verificationContext = CreateContext();
        Assert.Equal(1, await verificationContext.Sales.CountAsync());
        Assert.Equal(1, await verificationContext.OutboxMessages.CountAsync());
        Assert.Single(duplicate.DomainEvents);
    }

    [Fact]
    public async Task Processor_publishes_and_marks_the_message_as_processed()
    {
        await ResetDatabaseAsync();
        var clock = new MutableTimeProvider(Now);
        var sale = await PersistSaleAsync("SALE-PROCESSED", clock);
        var publisher = new RecordingPublisher();

        await using (var context = CreateContext())
        {
            var processed = await Processor(context, publisher, clock).ProcessBatchAsync();
            Assert.Equal(1, processed);
        }

        var published = Assert.Single(publisher.Published);
        Assert.Equal(sale.Id, published.SaleId);
        await using var verificationContext = CreateContext();
        var message = await verificationContext.OutboxMessages.SingleAsync();
        Assert.NotNull(message.ProcessedAt);
        Assert.Null(message.LockToken);
    }

    [Fact]
    public async Task Processor_retries_with_backoff_then_moves_the_message_to_dead_letter()
    {
        await ResetDatabaseAsync();
        var clock = new MutableTimeProvider(Now);
        await PersistSaleAsync("SALE-RETRY", clock);
        var publisher = new ThrowingPublisher();
        var options = new OutboxOptions
        {
            MaxAttempts = 2,
            BaseRetrySeconds = 5,
            MaxRetrySeconds = 30,
            LeaseSeconds = 30,
            BatchSize = 10
        };

        await using (var firstContext = CreateContext())
            Assert.Equal(1, await Processor(firstContext, publisher, clock, options).ProcessBatchAsync());

        await using (var firstFailureContext = CreateContext())
        {
            var firstFailure = await firstFailureContext.OutboxMessages.AsNoTracking().SingleAsync();
            Assert.Equal(2000, firstFailure.LastError!.Length);
        }

        await using (var beforeRetryContext = CreateContext())
            Assert.Equal(0, await Processor(beforeRetryContext, publisher, clock, options).ProcessBatchAsync());

        clock.Advance(TimeSpan.FromSeconds(5));
        await using (var retryContext = CreateContext())
            Assert.Equal(1, await Processor(retryContext, publisher, clock, options).ProcessBatchAsync());

        await using var verificationContext = CreateContext();
        var message = await verificationContext.OutboxMessages.SingleAsync();
        Assert.Equal(2, message.AttemptCount);
        Assert.NotNull(message.FailedAt);
        Assert.Null(message.ProcessedAt);
        Assert.Equal("Simulated audit outage.", message.LastError);

        var eventId = message.Id;
        await using (var replayContext = CreateContext())
        {
            var administration = new OutboxAdministration(replayContext, clock);
            var status = await administration.GetStatusAsync();
            Assert.Equal(1, status.DeadLetterCount);
            Assert.Equal(eventId, Assert.Single(status.DeadLetters).EventId);
            Assert.True(await administration.ReplayAsync(eventId));
        }

        var recoveredPublisher = new RecordingPublisher();
        await using (var recoveryContext = CreateContext())
            Assert.Equal(1, await Processor(recoveryContext, recoveredPublisher, clock, options).ProcessBatchAsync());

        Assert.Single(recoveredPublisher.Published);
    }

    [Fact]
    public async Task Concurrent_workers_publish_each_event_only_once()
    {
        await ResetDatabaseAsync();
        var clock = new MutableTimeProvider(Now);
        for (var index = 0; index < 8; index++)
            await PersistSaleAsync($"SALE-CONCURRENT-{index}", clock);

        var publisher = new RecordingPublisher();
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();

        await Task.WhenAll(
            Processor(firstContext, publisher, clock).ProcessBatchAsync(),
            Processor(secondContext, publisher, clock).ProcessBatchAsync());

        Assert.Equal(8, publisher.Published.Count);
        Assert.Equal(8, publisher.Published.Select(message => message.EventId).Distinct().Count());

        await using var verificationContext = CreateContext();
        Assert.Equal(8, await verificationContext.OutboxMessages.CountAsync(message => message.ProcessedAt != null));
    }

    [Fact]
    public async Task Worker_that_loses_its_lease_cannot_acknowledge_the_message()
    {
        await ResetDatabaseAsync();
        var clock = new MutableTimeProvider(Now);
        await PersistSaleAsync("SALE-LEASE", clock);
        var replacementToken = Guid.NewGuid();
        var publisher = new DelegatePublisher(async _ =>
        {
            await using var competingContext = CreateContext();
            var claimed = await competingContext.OutboxMessages.SingleAsync();
            claimed.Claim(replacementToken, Now.AddMinutes(2));
            await competingContext.SaveChangesAsync();
        });

        await using (var context = CreateContext())
            Assert.Equal(1, await Processor(context, publisher, clock).ProcessBatchAsync());

        await using var verificationContext = CreateContext();
        var message = await verificationContext.OutboxMessages.SingleAsync();
        Assert.Equal(replacementToken, message.LockToken);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public async Task Dead_letter_blocks_later_events_for_the_same_aggregate_until_replay()
    {
        await ResetDatabaseAsync();
        var clock = new MutableTimeProvider(Now);
        var saleId = Guid.NewGuid();
        await using (var seedContext = CreateContext())
        {
            seedContext.OutboxMessages.AddRange(
                OutboxMessage.From(new SaleCreatedEvent(saleId, 1, Now), 0, "order", Now),
                OutboxMessage.From(new SaleModifiedEvent(saleId, 2, Now.AddMinutes(1)), 0, "order", Now));
            await seedContext.SaveChangesAsync();
        }
        var options = new OutboxOptions { MaxAttempts = 1, BatchSize = 10 };

        await using (var failureContext = CreateContext())
            Assert.Equal(1, await Processor(
                failureContext,
                new ThrowingPublisher(),
                clock,
                options).ProcessBatchAsync());

        await using (var blockedContext = CreateContext())
            Assert.Equal(0, await Processor(
                blockedContext,
                new RecordingPublisher(),
                clock,
                options).ProcessBatchAsync());

        await using var verificationContext = CreateContext();
        var messages = await verificationContext.OutboxMessages
            .OrderBy(message => message.AggregateVersion)
            .ToListAsync();
        Assert.NotNull(messages[0].FailedAt);
        Assert.Equal(0, messages[1].AttemptCount);
    }

    [Fact]
    public async Task Processor_preserves_order_for_events_from_the_same_sale_version()
    {
        await ResetDatabaseAsync();
        var clock = new MutableTimeProvider(Now);
        var sale = await PersistSaleAsync("SALE-ORDER", clock);
        var publisher = new RecordingPublisher();

        await using (var firstContext = CreateContext())
            await Processor(firstContext, publisher, clock).ProcessBatchAsync();

        await using (var updateContext = CreateContext())
        {
            var persisted = await updateContext.Sales.Include(candidate => candidate.Items)
                .SingleAsync(candidate => candidate.Id == sale.Id);
            persisted.CancelItem(persisted.Items.Single().Id, Now.AddMinutes(1));
            await TestUnitOfWork.Create(updateContext, "order-test", clock).CommitAsync();
        }

        await using (var itemContext = CreateContext())
            Assert.Equal(1, await Processor(itemContext, publisher, clock).ProcessBatchAsync());
        await using (var saleContext = CreateContext())
            Assert.Equal(1, await Processor(saleContext, publisher, clock).ProcessBatchAsync());

        Assert.Equal(
            ["SaleCreatedEvent", "ItemCancelledEvent", "SaleCancelledEvent"],
            publisher.Published.Select(message => message.EventType));
    }

    private OutboxProcessor Processor(
        DefaultContext context,
        ISaleDomainEventPublisher publisher,
        TimeProvider clock,
        OutboxOptions? options = null) => new(
        context,
        publisher,
        clock,
        Options.Create(options ?? new OutboxOptions()),
        NullLogger<OutboxProcessor>.Instance);

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

    private async Task<Sale> PersistSaleAsync(string saleNumber, TimeProvider clock)
    {
        var sale = CreateSale(saleNumber, 1);
        await using var context = CreateContext();
        await new SaleRepository(context).AddAsync(sale);
        await TestUnitOfWork.Create(context, "integration-test", clock).CommitAsync();
        return sale;
    }

    private static Sale CreateSale(string saleNumber, int itemCount)
    {
        var items = Enumerable.Range(1, itemCount)
            .Select(index => SaleItemDraft.New(
                ExternalIdentity.Create($"PRODUCT-{index:000}", $"Product {index}"),
                1,
                10m))
            .ToArray();

        return Sale.Create(
            saleNumber,
            Now.AddHours(-1),
            ExternalIdentity.Create("CUSTOMER-001", "Example Customer"),
            ExternalIdentity.Create("BRANCH-001", "Fortaleza Branch"),
            items,
            Now);
    }

    private sealed class RecordingPublisher : ISaleDomainEventPublisher
    {
        public ConcurrentQueue<SaleEventMessage> Published { get; } = [];

        public Task PublishAsync(
            SaleEventMessage message,
            CancellationToken cancellationToken = default)
        {
            Published.Enqueue(message);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : ISaleDomainEventPublisher
    {
        private int _attempt;

        public Task PublishAsync(
            SaleEventMessage message,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                Interlocked.Increment(ref _attempt) == 1
                    ? "Simulated audit outage. " + new string('x', 2100)
                    : "Simulated audit outage.");
    }

    private sealed class DelegatePublisher(Func<SaleEventMessage, Task> publish) : ISaleDomainEventPublisher
    {
        public Task PublishAsync(
            SaleEventMessage message,
            CancellationToken cancellationToken = default) => publish(message);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
