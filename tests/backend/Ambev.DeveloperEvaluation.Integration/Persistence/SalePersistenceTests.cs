using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Data.Common;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class SalePersistenceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlFixture _database;

    public SalePersistenceTests(PostgreSqlFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Migrations_ShouldCreateSalesSchemaOnEmptyDatabase()
    {
        await ResetDatabaseAsync();

        await using var context = CreateContext();
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();

        Assert.Contains(appliedMigrations, migration => migration.EndsWith("_AddSalesPersistence"));
        Assert.True(await DatabaseObjectExistsAsync(
            context,
            "SELECT EXISTS (SELECT 1 FROM pg_indexes " +
            "WHERE indexname = 'UX_Sales_SaleNumber' AND indexdef LIKE 'CREATE UNIQUE INDEX%')"));
        Assert.True(await DatabaseObjectExistsAsync(
            context,
            "SELECT EXISTS (SELECT 1 FROM pg_indexes " +
            "WHERE indexname = 'UX_SaleItems_SaleId_ProductExternalId' " +
            "AND indexdef LIKE 'CREATE UNIQUE INDEX%')"));
        Assert.True(await DatabaseObjectExistsAsync(
            context,
            "SELECT EXISTS (SELECT 1 FROM pg_indexes " +
            "WHERE indexname = 'IX_Sales_Public_SaleDate_Id' AND indexdef LIKE '%WHERE%IsDeleted%')"));
        Assert.True(await DatabaseObjectExistsAsync(
            context,
            "SELECT EXISTS (SELECT 1 FROM pg_indexes " +
            "WHERE indexname = 'IX_Sales_Public_Status_SaleDate_Id' AND indexdef LIKE '%WHERE%IsDeleted%')"));
        Assert.True(await DatabaseObjectExistsAsync(
            context,
            "SELECT NOT EXISTS (SELECT 1 FROM pg_indexes " +
            "WHERE indexname IN ('IX_SaleItems_SaleId', 'IX_SaleItems_ProductExternalId'))"));
        Assert.True(await DatabaseObjectExistsAsync(
            context,
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns " +
            "WHERE table_name = 'SaleItems' AND column_name = 'UnitPrice' " +
            "AND numeric_precision = 18 AND numeric_scale = 2)"));
        Assert.True(await DatabaseObjectExistsAsync(
            context,
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns " +
            "WHERE table_name = 'SaleItems' AND column_name = 'DiscountRate' " +
            "AND numeric_precision = 5 AND numeric_scale = 4)"));
    }

    [Fact]
    public async Task Migration_ShouldPreserveUsersFromLegacySchema()
    {
        await using (var context = CreateContext())
        {
            await context.Database.EnsureDeletedAsync();
            var migrator = context.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20260924001411_AddUserTimestamps");

            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Users"
                    ("Id", "Username", "Password", "Email", "Phone", "Status", "Role", "CreatedAt", "UpdatedAt")
                VALUES
                    ({Guid.Parse("11111111-1111-1111-1111-111111111111")},
                     {"legacy-user"}, {"hashed-password"}, {"legacy@example.com"}, {"+5585999999999"},
                     {"Active"}, {"Customer"}, {Now.UtcDateTime}, NULL)
                """);

            await migrator.MigrateAsync();
        }

        await using var verificationContext = CreateContext();
        Assert.True(await verificationContext.Users.AnyAsync(user => user.Email == "legacy@example.com"));
        Assert.True(await DatabaseObjectExistsAsync(
            verificationContext,
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_name = 'Sales')"));
    }

    [Fact]
    public async Task Repository_ShouldRoundTripCompleteAggregateAndHistoricalSnapshots()
    {
        await ResetDatabaseAsync();
        var sale = CreateSale(
            "SALE-ROUNDTRIP",
            SaleItemDraft.New(Product("PRODUCT-001", "Original product"), 4, 10.25m),
            SaleItemDraft.New(Product("PRODUCT-002", "Cancelled product"), 10, 0.05m));
        sale.CancelItem(sale.Items[1].Id, Now.AddMinutes(1));
        sale.ClearDomainEvents();

        await using (var writeContext = CreateContext())
        {
            ISaleRepository repository = new SaleRepository(writeContext);
            IUnitOfWork unitOfWork = new UnitOfWork(writeContext);
            await repository.AddAsync(sale);
            await unitOfWork.CommitAsync();
        }

        await using var readContext = CreateContext();
        var persisted = await new SaleRepository(readContext).GetByIdAsync(sale.Id);

        Assert.NotNull(persisted);
        Assert.Equal("SALE-ROUNDTRIP", persisted.SaleNumber);
        Assert.Equal("CUSTOMER-001", persisted.Customer.ExternalId);
        Assert.Equal("Example Customer", persisted.Customer.Name);
        Assert.Equal("BRANCH-001", persisted.Branch.ExternalId);
        Assert.Equal(36.90m, persisted.TotalAmount);
        Assert.Equal(2, persisted.Items.Count);
        Assert.Equal("Original product", persisted.Items.Single(item => !item.IsCancelled).Product.Name);

        var cancelledItem = persisted.Items.Single(item => item.IsCancelled);
        Assert.Equal(0.40m, cancelledItem.TotalAmount);
        Assert.Equal(Now.AddMinutes(1), cancelledItem.CancelledAt);
        Assert.Empty(persisted.DomainEvents);
    }

    [Fact]
    public async Task SoftDelete_ShouldHideSaleButKeepAggregateForInternalOperations()
    {
        await ResetDatabaseAsync();
        var sale = CreateSale("SALE-DELETED", SaleItemDraft.New(Product("PRODUCT-001"), 1, 10m));

        await using (var writeContext = CreateContext())
        {
            var repository = new SaleRepository(writeContext);
            await repository.AddAsync(sale);
            await new UnitOfWork(writeContext).CommitAsync();

            sale.Delete(Now.AddMinutes(1));
            await new UnitOfWork(writeContext).CommitAsync();
        }

        await using var readContext = CreateContext();
        var repositoryForRead = new SaleRepository(readContext);

        Assert.Null(await repositoryForRead.GetByIdAsync(sale.Id));
        var deleted = await repositoryForRead.GetByIdIncludingDeletedAsync(sale.Id);
        Assert.NotNull(deleted);
        Assert.True(deleted.IsDeleted);
        Assert.Single(deleted.Items);
        Assert.True(await repositoryForRead.ExistsBySaleNumberAsync("SALE-DELETED"));
    }

    [Fact]
    public async Task Update_ShouldReplaceCustomerAndBranchSnapshotsWithoutChangingProductSnapshot()
    {
        await ResetDatabaseAsync();
        var sale = CreateSale(
            "SALE-SNAPSHOTS",
            SaleItemDraft.New(Product("PRODUCT-001", "Historical product"), 1, 10m));

        await using (var seedContext = CreateContext())
        {
            await new SaleRepository(seedContext).AddAsync(sale);
            await new UnitOfWork(seedContext).CommitAsync();
        }

        await using (var updateContext = CreateContext())
        {
            var persisted = await new SaleRepository(updateContext).GetByIdAsync(sale.Id);
            Assert.NotNull(persisted);
            var item = persisted.Items.Single();

            persisted.Update(
                persisted.SaleDate,
                ExternalIdentity.Create("CUSTOMER-002", "Replacement customer"),
                ExternalIdentity.Create("BRANCH-002", "Replacement branch"),
                [SaleItemDraft.Existing(item.Id, item.Product, item.Quantity, item.UnitPrice)],
                Now.AddMinutes(1));

            await new UnitOfWork(updateContext).CommitAsync();
        }

        await using var verificationContext = CreateContext();
        var updated = await new SaleRepository(verificationContext).GetByIdAsync(sale.Id);
        Assert.NotNull(updated);
        Assert.Equal("CUSTOMER-002", updated.Customer.ExternalId);
        Assert.Equal("Replacement customer", updated.Customer.Name);
        Assert.Equal("BRANCH-002", updated.Branch.ExternalId);
        Assert.Equal("Replacement branch", updated.Branch.Name);
        Assert.Equal("Historical product", updated.Items.Single().Product.Name);
        Assert.Equal(2, updated.Version);
    }

    [Fact]
    public async Task UniqueSaleNumber_ShouldIncludeSoftDeletedSales()
    {
        await ResetDatabaseAsync();
        var original = CreateSale("SALE-UNIQUE", SaleItemDraft.New(Product("PRODUCT-001"), 1, 10m));

        await using (var firstContext = CreateContext())
        {
            await new SaleRepository(firstContext).AddAsync(original);
            await new UnitOfWork(firstContext).CommitAsync();
            original.Delete(Now.AddMinutes(1));
            await new UnitOfWork(firstContext).CommitAsync();
        }

        await using (var duplicateContext = CreateContext())
        {
            await new SaleRepository(duplicateContext).AddAsync(
                CreateSale("SALE-UNIQUE", SaleItemDraft.New(Product("PRODUCT-002"), 1, 10m)));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => new UnitOfWork(duplicateContext).CommitAsync());
        }

        await using var verificationContext = CreateContext();
        Assert.Equal(1, await verificationContext.Sales.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Commit_ShouldRollbackWholeAggregateWhenItemViolatesDatabaseConstraint()
    {
        await ResetDatabaseAsync();
        var sale = CreateSale("SALE-ROLLBACK", SaleItemDraft.New(Product("PRODUCT-001"), 1, 10m));

        await using (var invalidContext = CreateContext())
        {
            await new SaleRepository(invalidContext).AddAsync(sale);
            invalidContext.Entry(sale.Items[0])
                .Property(item => item.Quantity)
                .CurrentValue = 21;

            await Assert.ThrowsAsync<DbUpdateException>(
                () => new UnitOfWork(invalidContext).CommitAsync());
        }

        await using var verificationContext = CreateContext();
        Assert.Equal(0, await verificationContext.Sales.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await verificationContext.Set<SaleItem>().CountAsync());
    }

    [Fact]
    public async Task ConcurrentUpdates_ShouldRejectStaleAggregateWithoutLostUpdate()
    {
        await ResetDatabaseAsync();
        var sale = CreateSale("SALE-CONCURRENT", SaleItemDraft.New(Product("PRODUCT-001"), 3, 10m));

        await using (var seedContext = CreateContext())
        {
            await new SaleRepository(seedContext).AddAsync(sale);
            await new UnitOfWork(seedContext).CommitAsync();
        }

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var first = await new SaleRepository(firstContext).GetByIdAsync(sale.Id);
        var second = await new SaleRepository(secondContext).GetByIdAsync(sale.Id);
        Assert.NotNull(first);
        Assert.NotNull(second);

        UpdateQuantity(first, 4, Now.AddMinutes(1));
        UpdateQuantity(second, 10, Now.AddMinutes(2));

        await new UnitOfWork(firstContext).CommitAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => new UnitOfWork(secondContext).CommitAsync());

        await using var verificationContext = CreateContext();
        var persisted = await new SaleRepository(verificationContext).GetByIdAsync(sale.Id);
        Assert.NotNull(persisted);
        Assert.Equal(4, persisted.Items.Single().Quantity);
        Assert.Equal(36m, persisted.TotalAmount);
        Assert.Equal(2, persisted.Version);
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

    private static Sale CreateSale(string saleNumber, params SaleItemDraft[] items)
    {
        return Sale.Create(
            saleNumber,
            Now.AddHours(-1),
            ExternalIdentity.Create("CUSTOMER-001", "Example Customer"),
            ExternalIdentity.Create("BRANCH-001", "Fortaleza Branch"),
            items,
            Now);
    }

    private static ExternalIdentity Product(string id, string name = "Example Product")
    {
        return ExternalIdentity.Create(id, name);
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

    private static async Task<bool> DatabaseObjectExistsAsync(DefaultContext context, string sql)
    {
        DbConnection connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
