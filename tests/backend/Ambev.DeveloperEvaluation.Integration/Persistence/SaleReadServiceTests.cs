using System.Data.Common;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class SaleReadServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlFixture _database;

    public SaleReadServiceTests(PostgreSqlFixture database) => _database = database;

    [Fact]
    public async Task List_projects_a_stable_page_with_two_select_commands()
    {
        await ResetDatabaseAsync();
        var older = CreateSale("SALE-OLDER", Now.AddDays(-2), "C1", "B1", 10m);
        var newerA = CreateSale("SALE-NEWER-A", Now.AddDays(-1), "C1", "B1", 20m);
        var newerB = CreateSale("SALE-NEWER-B", Now.AddDays(-1), "C1", "B1", 30m);
        await SeedAsync(older, newerA, newerB);
        var interceptor = new SelectCommandCounter();
        await using var context = CreateContext(interceptor);
        var service = new SaleReadService(context);

        var result = await service.ListAsync(Criteria(pageSize: 2));

        var expectedFirstPage = new[] { newerA, newerB }.OrderBy(sale => sale.Id).Select(sale => sale.Id);
        Assert.Equal(expectedFirstPage, result.Data.Select(sale => sale.Id));
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, interceptor.Count);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task List_returns_correct_metadata_for_last_beyond_and_empty_pages()
    {
        await ResetDatabaseAsync();
        await using (var emptyContext = CreateContext())
        {
            var empty = await new SaleReadService(emptyContext).ListAsync(Criteria(pageSize: 2) with { Page = 3 });
            Assert.Empty(empty.Data);
            Assert.Equal(0, empty.TotalItems);
            Assert.Equal(0, empty.TotalPages);
            Assert.Equal(3, empty.CurrentPage);
        }

        await SeedAsync(
            CreateSale("SALE-1", Now.AddDays(-1), "C1", "B1", 10m),
            CreateSale("SALE-2", Now.AddDays(-2), "C1", "B1", 10m),
            CreateSale("SALE-3", Now.AddDays(-3), "C1", "B1", 10m));
        await using var context = CreateContext();
        var service = new SaleReadService(context);

        var lastPage = await service.ListAsync(Criteria(pageSize: 2) with { Page = 2 });
        var beyond = await service.ListAsync(Criteria(pageSize: 2) with { Page = 3 });

        Assert.Single(lastPage.Data);
        Assert.Equal(3, lastPage.TotalItems);
        Assert.Equal(2, lastPage.TotalPages);
        Assert.Empty(beyond.Data);
        Assert.Equal(3, beyond.TotalItems);
        Assert.Equal(2, beyond.TotalPages);
    }

    [Fact]
    public async Task List_escapes_like_metacharacters_and_excludes_deleted_sales()
    {
        await ResetDatabaseAsync();
        var literal = CreateSale("PROMO_%_2026", Now.AddDays(-1), "C1", "B1", 10m);
        var similar = CreateSale("PROMO-AB-2026", Now.AddDays(-1), "C1", "B1", 10m);
        var deleted = CreateSale("DELETED-PROMO_%", Now.AddDays(-1), "C1", "B1", 10m);
        deleted.Delete(Now.AddMinutes(1));
        await SeedAsync(literal, similar, deleted);
        await using var context = CreateContext();
        var service = new SaleReadService(context);
        var criteria = Criteria() with { SaleNumber = new SaleNumberFilter("%_", true, true) };

        var result = await service.ListAsync(criteria);

        Assert.Equal(literal.Id, Assert.Single(result.Data).Id);
        Assert.Equal(1, result.TotalItems);
    }

    [Fact]
    public async Task List_combines_filter_groups_with_and_and_repeated_values_with_or()
    {
        await ResetDatabaseAsync();
        var match = CreateSale("SALE-MATCH", Now.AddDays(-2), "C1", "B1", 10m);
        var wrongBranch = CreateSale("SALE-WRONG-BRANCH", Now.AddDays(-2), "C2", "B2", 10m);
        var wrongAmount = CreateSale("SALE-WRONG-AMOUNT", Now.AddDays(-2), "C2", "B1", 30m);
        var wrongDate = CreateSale("SALE-WRONG-DATE", Now.AddDays(-5), "C1", "B1", 10m);
        await SeedAsync(match, wrongBranch, wrongAmount, wrongDate);
        await using var context = CreateContext();
        var service = new SaleReadService(context);
        var criteria = Criteria() with
        {
            CustomerExternalIds = ["C1", "C2"],
            BranchExternalIds = ["B1"],
            CancellationStates = [false],
            MinimumSaleDate = Now.AddDays(-3),
            MaximumTotalAmount = 20m
        };

        var result = await service.ListAsync(criteria);

        Assert.Equal(match.Id, Assert.Single(result.Data).Id);
    }

    [Fact]
    public async Task List_supports_every_sale_number_boundary_amount_date_state_and_order_direction()
    {
        await ResetDatabaseAsync();
        var alpha = CreateSale("ALPHA-MATCH", Now.AddDays(-2), "C1", "B1", 10m);
        var beta = CreateSale("MATCH-BETA", Now.AddDays(-1), "C2", "B2", 20m);
        var cancelled = CreateSale("MATCH-CANCELLED", Now.AddHours(-1), "C1", "B1", 30m);
        cancelled.Cancel(Now);
        await SeedAsync(alpha, beta, cancelled);
        await using var context = CreateContext();
        var service = new SaleReadService(context);

        Assert.Equal(alpha.Id, Assert.Single((await service.ListAsync(
            Criteria() with { SaleNumber = new SaleNumberFilter("ALPHA-MATCH", false, false) })).Data).Id);
        Assert.Equal(alpha.Id, Assert.Single((await service.ListAsync(
            Criteria() with { SaleNumber = new SaleNumberFilter("ALPHA", false, true) })).Data).Id);
        Assert.Equal(beta.Id, Assert.Single((await service.ListAsync(
            Criteria() with { SaleNumber = new SaleNumberFilter("BETA", true, false) })).Data).Id);

        var bounded = await service.ListAsync(Criteria() with
        {
            CancellationStates = [true],
            MaximumSaleDate = Now,
            MaximumTotalAmount = 0m
        });
        Assert.Equal(cancelled.Id, Assert.Single(bounded.Data).Id);
        var minimum = await service.ListAsync(Criteria() with
        {
            CancellationStates = [false],
            MinimumTotalAmount = 15m
        });
        Assert.Equal(beta.Id, Assert.Single(minimum.Data).Id);

        var orders = new[]
        {
            new[] { new SaleOrder(SaleOrderField.SaleDate, SortDirection.Ascending), new SaleOrder(SaleOrderField.Id, SortDirection.Descending) },
            new[] { new SaleOrder(SaleOrderField.SaleNumber, SortDirection.Descending), new SaleOrder(SaleOrderField.Id, SortDirection.Ascending) },
            new[] { new SaleOrder(SaleOrderField.TotalAmount, SortDirection.Ascending), new SaleOrder(SaleOrderField.Id, SortDirection.Descending) },
            new[] { new SaleOrder(SaleOrderField.Id, SortDirection.Descending) }
        };
        foreach (var order in orders)
            Assert.Equal(3, (await service.ListAsync(Criteria() with { Order = order })).Data.Count);
    }

    private SaleListCriteria Criteria(int pageSize = 10) => new(
        1,
        pageSize,
        [
            new SaleOrder(SaleOrderField.SaleDate, SortDirection.Descending),
            new SaleOrder(SaleOrderField.Id, SortDirection.Ascending)
        ],
        null,
        [],
        [],
        [],
        null,
        null,
        null,
        null);

    private Sale CreateSale(
        string number,
        DateTimeOffset saleDate,
        string customerId,
        string branchId,
        decimal unitPrice) => Sale.Create(
            number,
            saleDate,
            ExternalIdentity.Create(customerId, $"Customer {customerId}"),
            ExternalIdentity.Create(branchId, $"Branch {branchId}"),
            [SaleItemDraft.New(ExternalIdentity.Create($"PRODUCT-{number}", "Product"), 1, unitPrice)],
            Now);

    private async Task SeedAsync(params Sale[] sales)
    {
        await using var context = CreateContext();
        context.Sales.AddRange(sales);
        await context.SaveChangesAsync();
    }

    private DefaultContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseNpgsql(
                _database.ConnectionString,
                provider => provider.MigrationsAssembly(typeof(DefaultContext).Assembly.FullName))
            .AddInterceptors(interceptors)
            .Options;
        return new DefaultContext(options);
    }

    private async Task ResetDatabaseAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    private sealed class SelectCommandCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Count++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}
