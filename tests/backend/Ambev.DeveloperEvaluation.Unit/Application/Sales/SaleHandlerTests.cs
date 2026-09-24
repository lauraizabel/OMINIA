using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public sealed class SaleHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_persists_once_and_returns_server_calculated_values()
    {
        var repository = Substitute.For<ISaleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new CreateSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.Version.Should().Be(1);
        result.TotalAmount.Should().Be(36m);
        result.Items.Single().DiscountRate.Should().Be(0.10m);
        await repository.Received(1).AddAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_with_normalized_duplicate_number_does_not_write()
    {
        var repository = Substitute.For<ISaleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.ExistsBySaleNumberAsync("SALE-001", Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CreateSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now));

        var action = () => handler.Handle(CreateCommand("  SALE-001  "), CancellationToken.None);

        var exception = await action.Should().ThrowAsync<DomainConflictException>();
        exception.Which.Code.Should().Be(DomainErrorCodes.Sale.NumberAlreadyExists);
        await repository.DidNotReceive().AddAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_unknown_sale_returns_domain_not_found()
    {
        var repository = Substitute.For<ISaleRepository>();
        var id = Guid.NewGuid();
        var handler = new GetSaleHandler(repository);

        var action = () => handler.Handle(new GetSaleQuery(id), CancellationToken.None);

        var exception = await action.Should().ThrowAsync<DomainNotFoundException>();
        exception.Which.Code.Should().Be(DomainErrorCodes.Sale.NotFound);
    }

    [Fact]
    public async Task Update_with_stale_version_does_not_mutate_or_write()
    {
        var sale = CreateAggregate();
        var repository = Substitute.For<ISaleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        var handler = new UpdateSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddMinutes(1)));

        var action = () => handler.Handle(UpdateCommand(sale, sale.Version + 1, 5), CancellationToken.None);

        var exception = await action.Should().ThrowAsync<DomainConcurrencyException>();
        exception.Which.Code.Should().Be(DomainErrorCodes.Sale.VersionConflict);
        sale.Version.Should().Be(1);
        sale.Items.Single().Quantity.Should().Be(4);
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Identical_update_is_a_no_op_and_skips_the_write()
    {
        var sale = CreateAggregate();
        var repository = Substitute.For<ISaleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        var handler = new UpdateSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddMinutes(1)));

        var result = await handler.Handle(UpdateCommand(sale, sale.Version, 4), CancellationToken.None);

        result.Version.Should().Be(1);
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Changed_update_commits_once_and_returns_new_version()
    {
        var sale = CreateAggregate();
        var repository = Substitute.For<ISaleRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        var handler = new UpdateSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddMinutes(1)));

        var result = await handler.Handle(UpdateCommand(sale, sale.Version, 10), CancellationToken.None);

        result.Version.Should().Be(2);
        result.Items.Single().DiscountRate.Should().Be(0.20m);
        result.TotalAmount.Should().Be(80m);
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    private static CreateSaleCommand CreateCommand(string number = "SALE-001") => new(
        number,
        Now.AddHours(-1),
        Identity("CUSTOMER-001", "Customer"),
        Identity("BRANCH-001", "Branch"),
        [new SaleItemInput(null, Identity("PRODUCT-001", "Product"), 4, 10m)]);

    private static Sale CreateAggregate() => Sale.Create(
        "SALE-001",
        Now.AddHours(-1),
        ExternalIdentity.Create("CUSTOMER-001", "Customer"),
        ExternalIdentity.Create("BRANCH-001", "Branch"),
        [SaleItemDraft.New(ExternalIdentity.Create("PRODUCT-001", "Product"), 4, 10m)],
        Now);

    private static UpdateSaleCommand UpdateCommand(Sale sale, long expectedVersion, int quantity) => new(
        sale.Id,
        expectedVersion,
        sale.SaleDate,
        Identity(sale.Customer.ExternalId, sale.Customer.Name),
        Identity(sale.Branch.ExternalId, sale.Branch.Name),
        [new SaleItemInput(
            sale.Items.Single().Id,
            Identity(sale.Items.Single().Product.ExternalId, sale.Items.Single().Product.Name),
            quantity,
            sale.Items.Single().UnitPrice)]);

    private static ExternalIdentityInput Identity(string id, string name) => new(id, name);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
