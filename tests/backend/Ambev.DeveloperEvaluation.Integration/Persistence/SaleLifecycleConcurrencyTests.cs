using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class SaleLifecycleConcurrencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlFixture _database;

    public SaleLifecycleConcurrencyTests(PostgreSqlFixture database) => _database = database;

    [Fact]
    public async Task Cancellation_wins_over_concurrent_update_without_lost_changes()
    {
        var sale = await SeedSaleAsync("SALE-CANCEL-VS-UPDATE");
        await using var cancellationContext = CreateContext();
        await using var updateContext = CreateContext();
        var cancellationCopy = await new SaleRepository(cancellationContext).GetByIdAsync(sale.Id);
        var updateCopy = await new SaleRepository(updateContext).GetByIdAsync(sale.Id);
        Assert.NotNull(cancellationCopy);
        Assert.NotNull(updateCopy);

        cancellationCopy.Cancel(Now.AddMinutes(1));
        UpdateQuantity(updateCopy, 4, Now.AddMinutes(2));
        await TestUnitOfWork.Create(cancellationContext).CommitAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => TestUnitOfWork.Create(updateContext).CommitAsync());

        await using var verificationContext = CreateContext();
        var persisted = await new SaleRepository(verificationContext).GetByIdAsync(sale.Id);
        Assert.NotNull(persisted);
        Assert.True(persisted.IsCancelled);
        Assert.Equal(0m, persisted.TotalAmount);
        Assert.All(persisted.Items, item => Assert.True(item.IsCancelled));
        Assert.Equal(2, persisted.Version);
    }

    [Fact]
    public async Task Deletion_wins_over_concurrent_cancellation_and_hides_sale()
    {
        var sale = await SeedSaleAsync("SALE-DELETE-VS-CANCEL");
        await using var deletionContext = CreateContext();
        await using var cancellationContext = CreateContext();
        var deletionCopy = await new SaleRepository(deletionContext).GetByIdAsync(sale.Id);
        var cancellationCopy = await new SaleRepository(cancellationContext).GetByIdAsync(sale.Id);
        Assert.NotNull(deletionCopy);
        Assert.NotNull(cancellationCopy);

        deletionCopy.Delete(Now.AddMinutes(1));
        cancellationCopy.Cancel(Now.AddMinutes(2));
        await TestUnitOfWork.Create(deletionContext).CommitAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => TestUnitOfWork.Create(cancellationContext).CommitAsync());

        await using var verificationContext = CreateContext();
        var repository = new SaleRepository(verificationContext);
        Assert.Null(await repository.GetByIdAsync(sale.Id));
        var tombstone = await repository.GetByIdIncludingDeletedAsync(sale.Id);
        Assert.NotNull(tombstone);
        Assert.True(tombstone.IsDeleted);
        Assert.False(tombstone.IsCancelled);
        Assert.Equal(2, tombstone.Version);
    }

    private async Task<Sale> SeedSaleAsync(string number)
    {
        await using var resetContext = CreateContext();
        await resetContext.Database.EnsureDeletedAsync();
        await resetContext.Database.MigrateAsync();

        var sale = Sale.Create(
            number,
            Now.AddHours(-1),
            ExternalIdentity.Create("CUSTOMER-001", "Customer"),
            ExternalIdentity.Create("BRANCH-001", "Branch"),
            [SaleItemDraft.New(ExternalIdentity.Create("PRODUCT-001", "Product"), 1, 10m)],
            Now);
        resetContext.Sales.Add(sale);
        await resetContext.SaveChangesAsync();
        return sale;
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

    private static void UpdateQuantity(Sale sale, int quantity, DateTimeOffset now)
    {
        var item = sale.Items.Single();
        sale.Update(
            sale.SaleDate,
            sale.Customer,
            sale.Branch,
            [SaleItemDraft.Existing(item.Id, item.Product, quantity, item.UnitPrice)],
            now);
    }
}
