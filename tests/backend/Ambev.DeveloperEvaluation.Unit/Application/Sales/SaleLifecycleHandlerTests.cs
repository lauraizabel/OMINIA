using Ambev.DeveloperEvaluation.Application.Sales.CancelSale;
using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Application.Sales.DeleteSale;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public sealed class SaleLifecycleHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Cancel_sale_commits_once_and_returns_cancelled_resource()
    {
        var sale = CreateSale(2);
        var (repository, unitOfWork) = Dependencies();
        repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        var handler = new CancelSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddMinutes(1)));

        var result = await handler.Handle(new CancelSaleCommand(sale.Id, 1), CancellationToken.None);

        result.IsCancelled.Should().BeTrue();
        result.TotalAmount.Should().Be(0m);
        result.Items.Should().OnlyContain(item => item.IsCancelled && item.EffectiveAmount == 0m);
        result.Version.Should().Be(2);
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repeated_sale_cancellation_with_current_version_is_a_no_op()
    {
        var sale = CreateSale(1);
        sale.Cancel(Now.AddMinutes(1));
        var (repository, unitOfWork) = Dependencies();
        repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        var handler = new CancelSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddMinutes(2)));

        var result = await handler.Handle(new CancelSaleCommand(sale.Id, 2), CancellationToken.None);

        result.Version.Should().Be(2);
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repeated_sale_cancellation_with_stale_version_fails_before_mutation()
    {
        var sale = CreateSale(1);
        sale.Cancel(Now.AddMinutes(1));
        var (repository, unitOfWork) = Dependencies();
        repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        var handler = new CancelSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddMinutes(2)));

        var action = () => handler.Handle(new CancelSaleCommand(sale.Id, 1), CancellationToken.None);

        await action.Should().ThrowAsync<DomainConcurrencyException>();
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_item_updates_total_and_commits_once()
    {
        var sale = CreateSale(2);
        var item = sale.Items[0];
        var remainingTotal = sale.Items[1].TotalAmount;
        var (repository, unitOfWork) = Dependencies();
        repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        var handler = new CancelSaleItemHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddMinutes(1)));

        var result = await handler.Handle(
            new CancelSaleItemCommand(sale.Id, item.Id, 1),
            CancellationToken.None);

        result.Items.Single(candidate => candidate.Id == item.Id).IsCancelled.Should().BeTrue();
        result.TotalAmount.Should().Be(remainingTotal);
        result.IsCancelled.Should().BeFalse();
        result.Version.Should().Be(2);
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelling_last_item_also_cancels_sale()
    {
        var sale = CreateSale(1);
        var item = sale.Items.Single();
        var (repository, unitOfWork) = Dependencies();
        repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        var handler = new CancelSaleItemHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddMinutes(1)));

        var result = await handler.Handle(
            new CancelSaleItemCommand(sale.Id, item.Id, 1),
            CancellationToken.None);

        result.IsCancelled.Should().BeTrue();
        result.TotalAmount.Should().Be(0m);
        result.Version.Should().Be(2);
    }

    [Fact]
    public async Task Repeated_item_cancellation_with_current_version_is_a_no_op()
    {
        var sale = CreateSale(2);
        var item = sale.Items[0];
        sale.CancelItem(item.Id, Now.AddMinutes(1));
        var (repository, unitOfWork) = Dependencies();
        repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        var handler = new CancelSaleItemHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddMinutes(2)));

        var result = await handler.Handle(
            new CancelSaleItemCommand(sale.Id, item.Id, 2),
            CancellationToken.None);

        result.Version.Should().Be(2);
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelling_unknown_item_does_not_write()
    {
        var sale = CreateSale(1);
        var (repository, unitOfWork) = Dependencies();
        repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        var handler = new CancelSaleItemHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddMinutes(1)));

        var action = () => handler.Handle(
            new CancelSaleItemCommand(sale.Id, Guid.NewGuid(), 1),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainNotFoundException>();
        sale.Version.Should().Be(1);
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_soft_deletes_and_commits_once()
    {
        var sale = CreateSale(1);
        var (repository, unitOfWork) = Dependencies();
        repository.GetByIdIncludingDeletedAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        var handler = new DeleteSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddMinutes(1)));

        await handler.Handle(new DeleteSaleCommand(sale.Id, 1), CancellationToken.None);

        sale.IsDeleted.Should().BeTrue();
        sale.Version.Should().Be(2);
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repeated_delete_accepts_only_last_pre_deletion_version()
    {
        var sale = CreateSale(1);
        sale.Delete(Now.AddMinutes(1));
        var (repository, unitOfWork) = Dependencies();
        repository.GetByIdIncludingDeletedAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        var handler = new DeleteSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddMinutes(2)));

        await handler.Handle(new DeleteSaleCommand(sale.Id, 1), CancellationToken.None);
        var wrongVersion = () => handler.Handle(new DeleteSaleCommand(sale.Id, 2), CancellationToken.None);

        await wrongVersion.Should().ThrowAsync<DomainConcurrencyException>();
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deleting_unknown_sale_returns_not_found_without_write()
    {
        var (repository, unitOfWork) = Dependencies();
        var handler = new DeleteSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now));

        var action = () => handler.Handle(new DeleteSaleCommand(Guid.NewGuid(), 1), CancellationToken.None);

        await action.Should().ThrowAsync<DomainNotFoundException>();
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    private static (ISaleRepository Repository, IUnitOfWork UnitOfWork) Dependencies() =>
        (Substitute.For<ISaleRepository>(), Substitute.For<IUnitOfWork>());

    private static Sale CreateSale(int itemCount) => Sale.Create(
        "SALE-LIFECYCLE",
        Now.AddHours(-1),
        ExternalIdentity.Create("CUSTOMER-001", "Customer"),
        ExternalIdentity.Create("BRANCH-001", "Branch"),
        Enumerable.Range(1, itemCount).Select(index => SaleItemDraft.New(
            ExternalIdentity.Create($"PRODUCT-{index}", $"Product {index}"),
            1,
            10m)),
        Now);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
