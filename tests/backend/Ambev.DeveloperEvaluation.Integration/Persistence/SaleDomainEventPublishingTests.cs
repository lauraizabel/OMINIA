using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.EntityFrameworkCore;
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
    public async Task Commits_ShouldPublishEverySaleEventInOrderAndOnlyAfterPersistence()
    {
        await ResetDatabaseAsync();
        var sale = CreateSale("SALE-EVENTS", 2);
        var publisher = new RecordingPublisher(async domainEvent =>
        {
            await using var verificationContext = CreateContext();
            return await verificationContext.Sales
                .IgnoreQueryFilters()
                .AnyAsync(candidate => candidate.Id == domainEvent.SaleId);
        });

        await using var context = CreateContext();
        await new SaleRepository(context).AddAsync(sale);
        var unitOfWork = TestUnitOfWork.Create(context, publisher, "correlation-123");

        await unitOfWork.CommitAsync();

        var firstItem = sale.Items[0];
        var secondItem = sale.Items[1];
        sale.Update(
            sale.SaleDate,
            sale.Customer,
            sale.Branch,
            [
                SaleItemDraft.Existing(firstItem.Id, firstItem.Product, 4, firstItem.UnitPrice),
                SaleItemDraft.Existing(secondItem.Id, secondItem.Product, secondItem.Quantity, secondItem.UnitPrice)
            ],
            Now.AddMinutes(1));
        await unitOfWork.CommitAsync();

        sale.CancelItem(firstItem.Id, Now.AddMinutes(2));
        await unitOfWork.CommitAsync();

        sale.Cancel(Now.AddMinutes(3));
        await unitOfWork.CommitAsync();

        Assert.False(sale.Cancel(Now.AddMinutes(4)));
        await unitOfWork.CommitAsync();

        Assert.Collection(
            publisher.Published,
            published => Assert.IsType<SaleCreatedEvent>(published.DomainEvent),
            published => Assert.IsType<SaleModifiedEvent>(published.DomainEvent),
            published => Assert.IsType<ItemCancelledEvent>(published.DomainEvent),
            published => Assert.IsType<SaleCancelledEvent>(published.DomainEvent));
        Assert.Equal([1L, 2L, 3L, 4L], publisher.Published.Select(item => item.DomainEvent.Version));
        Assert.All(publisher.Published, item => Assert.Equal("correlation-123", item.CorrelationId));
        Assert.All(publisher.Published, item => Assert.True(item.WasPersistedBeforePublication));
        Assert.Equal(4, publisher.Published.Select(item => item.DomainEvent.EventId).Distinct().Count());
        Assert.Empty(sale.DomainEvents);
    }

    [Fact]
    public async Task FailedCommit_ShouldNotPublishOrClearDomainEvents()
    {
        await ResetDatabaseAsync();

        await using (var seedContext = CreateContext())
        {
            await new SaleRepository(seedContext).AddAsync(CreateSale("SALE-DUPLICATE", 1));
            await TestUnitOfWork.Create(seedContext).CommitAsync();
        }

        var duplicate = CreateSale("SALE-DUPLICATE", 1);
        var publisher = new RecordingPublisher();
        await using var duplicateContext = CreateContext();
        await new SaleRepository(duplicateContext).AddAsync(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => TestUnitOfWork.Create(duplicateContext, publisher).CommitAsync());

        Assert.Empty(publisher.Published);
        Assert.Single(duplicate.DomainEvents);
    }

    [Fact]
    public async Task PublisherFailureAfterCommit_ShouldNotReportThePersistedWriteAsFailed()
    {
        await ResetDatabaseAsync();
        var sale = CreateSale("SALE-PUBLISHER-FAILURE", 1);

        await using (var context = CreateContext())
        {
            await new SaleRepository(context).AddAsync(sale);
            var affectedRows = await TestUnitOfWork.Create(context, new ThrowingPublisher()).CommitAsync();
            Assert.True(affectedRows > 0);
            Assert.Empty(sale.DomainEvents);
        }

        await using var verificationContext = CreateContext();
        Assert.True(await verificationContext.Sales.AnyAsync(candidate => candidate.Id == sale.Id));
    }

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
        private readonly Func<SaleDomainEvent, Task<bool>> _persistenceCheck;

        public RecordingPublisher(Func<SaleDomainEvent, Task<bool>>? persistenceCheck = null)
        {
            _persistenceCheck = persistenceCheck ?? (_ => Task.FromResult(true));
        }

        public List<PublishedEvent> Published { get; } = [];

        public async Task PublishAsync(
            SaleDomainEvent domainEvent,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            Published.Add(new PublishedEvent(
                domainEvent,
                correlationId,
                await _persistenceCheck(domainEvent)));
        }
    }

    private sealed class ThrowingPublisher : ISaleDomainEventPublisher
    {
        public Task PublishAsync(
            SaleDomainEvent domainEvent,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated publisher failure.");
        }
    }

    private sealed record PublishedEvent(
        SaleDomainEvent DomainEvent,
        string CorrelationId,
        bool WasPersistedBeforePublication);
}
