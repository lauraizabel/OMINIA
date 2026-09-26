using Ambev.DeveloperEvaluation.Application.Sales.CancelSale;
using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.DeleteSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public sealed class SaleCommandValidatorTests
{
    [Fact]
    public void Create_validator_accepts_a_complete_sale()
    {
        new CreateSaleCommandValidator().Validate(ValidCreate()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_validator_reports_required_aggregate_fields_and_null_items()
    {
        var command = new CreateSaleCommand(
            string.Empty,
            default,
            null!,
            null!,
            new SaleItemInput[] { null! });

        var result = new CreateSaleCommandValidator().Validate(command);

        result.Errors.Select(error => error.ErrorCode).Should().Contain([
            DomainErrorCodes.Sale.NumberRequired,
            DomainErrorCodes.Sale.DateRequired,
            DomainErrorCodes.Sale.CustomerRequired,
            DomainErrorCodes.Sale.BranchRequired,
            DomainErrorCodes.SaleItem.NullItem
        ]);
    }

    [Fact]
    public void Create_validator_enforces_identity_item_and_collection_limits()
    {
        var invalidItem = new SaleItemInput(
            null,
            new ExternalIdentityInput(string.Empty, string.Empty),
            0,
            1.234m);
        var items = Enumerable.Repeat(invalidItem, Sale.MaximumItems + 1).ToArray();
        var command = ValidCreate() with
        {
            SaleNumber = new string('X', Sale.SaleNumberMaximumLength + 1),
            Customer = new ExternalIdentityInput(new string('X', 101), new string('X', 201)),
            Items = items
        };

        var codes = new CreateSaleCommandValidator().Validate(command).Errors
            .Select(error => error.ErrorCode).ToArray();

        codes.Should().Contain(DomainErrorCodes.Sale.NumberTooLong);
        codes.Should().Contain(DomainErrorCodes.Sale.TooManyItems);
        codes.Should().Contain(DomainErrorCodes.ExternalIdentity.InvalidId);
        codes.Should().Contain(DomainErrorCodes.ExternalIdentity.InvalidName);
        codes.Should().Contain(DomainErrorCodes.SaleItem.QuantityOutOfRange);
        codes.Should().Contain(DomainErrorCodes.SaleItem.UnitPriceScaleExceeded);
    }

    [Fact]
    public void Update_validator_checks_identity_version_and_shared_sale_inputs()
    {
        var command = new UpdateSaleCommand(
            Guid.Empty,
            0,
            default,
            new ExternalIdentityInput("", ""),
            new ExternalIdentityInput("", ""),
            []);

        var codes = new UpdateSaleCommandValidator().Validate(command).Errors
            .Select(error => error.ErrorCode).ToArray();

        codes.Should().Contain(DomainErrorCodes.Sale.InvalidId);
        codes.Should().Contain(DomainErrorCodes.Sale.VersionConflict);
        codes.Should().Contain(DomainErrorCodes.Sale.DateRequired);
        codes.Should().Contain(DomainErrorCodes.Sale.ItemsRequired);
    }

    [Fact]
    public void Lifecycle_validators_reject_empty_identifiers_and_versions()
    {
        new CancelSaleCommandValidator().Validate(new CancelSaleCommand(Guid.Empty, 0)).IsValid
            .Should().BeFalse();
        new CancelSaleItemCommandValidator().Validate(
                new CancelSaleItemCommand(Guid.Empty, Guid.Empty, 0)).Errors
            .Should().HaveCount(3);
        new DeleteSaleCommandValidator().Validate(new DeleteSaleCommand(Guid.Empty, 0)).IsValid
            .Should().BeFalse();
        new GetSaleQueryValidator().Validate(new GetSaleQuery(Guid.Empty)).IsValid
            .Should().BeFalse();
    }

    [Fact]
    public void Lifecycle_validators_accept_valid_identifiers_and_versions()
    {
        var id = Guid.NewGuid();
        new CancelSaleCommandValidator().Validate(new CancelSaleCommand(id, 1)).IsValid
            .Should().BeTrue();
        new CancelSaleItemCommandValidator().Validate(new CancelSaleItemCommand(id, id, 1)).IsValid
            .Should().BeTrue();
        new DeleteSaleCommandValidator().Validate(new DeleteSaleCommand(id, 1)).IsValid
            .Should().BeTrue();
        new GetSaleQueryValidator().Validate(new GetSaleQuery(id)).IsValid
            .Should().BeTrue();
    }

    private static CreateSaleCommand ValidCreate() => new(
        "SALE-1",
        DateTimeOffset.UtcNow,
        new ExternalIdentityInput("CUSTOMER-1", "Customer"),
        new ExternalIdentityInput("BRANCH-1", "Branch"),
        [new SaleItemInput(null, new ExternalIdentityInput("PRODUCT-1", "Product"), 4, 10m)]);
}
