using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Sales;

public sealed class SaleTests
{
    public static TheoryData<int, int, decimal> DiscountTransitionCases => new()
    {
        { 3, 4, 0.10m },
        { 9, 10, 0.20m },
        { 10, 9, 0.10m },
        { 4, 3, 0m }
    };

    [Fact]
    public void Create_ShouldNormalizeDataCalculateTotalsAndRaiseEvent()
    {
        var saleDate = new DateTimeOffset(2026, 9, 23, 14, 0, 0, TimeSpan.FromHours(-3));

        var sale = Sale.Create(
            " sale-0001 ",
            saleDate,
            SaleTestData.Customer(" CUSTOMER-001 ", " Example Customer "),
            SaleTestData.Branch(),
            [SaleTestData.NewItem(1, 4, 10m)],
            SaleTestData.Now);

        sale.SaleNumber.Should().Be("SALE-0001");
        sale.SaleDate.Should().Be(saleDate.ToUniversalTime());
        sale.Customer.ExternalId.Should().Be("CUSTOMER-001");
        sale.TotalAmount.Should().Be(36m);
        sale.Version.Should().Be(1);
        sale.CreatedAt.Should().Be(SaleTestData.Now);
        sale.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SaleCreatedEvent>();
    }

    [Fact]
    public void Create_ShouldCalculateEachProductTierIndependently()
    {
        var sale = SaleTestData.CreateSale(
            SaleTestData.NewItem(1, 3, 10m),
            SaleTestData.NewItem(2, 4, 10m));

        sale.Items[0].DiscountRate.Should().Be(0m);
        sale.Items[1].DiscountRate.Should().Be(0.10m);
        sale.TotalAmount.Should().Be(66m);
    }

    [Theory]
    [InlineData("customer", DomainErrorCodes.Sale.CustomerRequired)]
    [InlineData("branch", DomainErrorCodes.Sale.BranchRequired)]
    [InlineData("product", DomainErrorCodes.SaleItem.ProductRequired)]
    public void Create_ShouldReturnSpecificCodeWhenRequiredIdentityIsMissing(
        string missingIdentity,
        string expectedCode)
    {
        var customer = missingIdentity == "customer" ? null : SaleTestData.Customer();
        var branch = missingIdentity == "branch" ? null : SaleTestData.Branch();
        var product = missingIdentity == "product" ? null : SaleTestData.Product(1);

        var action = () => Sale.Create(
            "SALE-0001",
            SaleTestData.Now,
            customer,
            branch,
            [SaleItemDraft.New(product!, 1, 10m)],
            SaleTestData.Now);

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void Create_ShouldRejectDuplicateProductAfterTrimming()
    {
        var first = ExternalIdentity.Create(" PRODUCT-001 ", "First description");
        var duplicate = ExternalIdentity.Create("PRODUCT-001", "Other description");

        var action = () => Sale.Create(
            "SALE-0001",
            SaleTestData.Now,
            SaleTestData.Customer(),
            SaleTestData.Branch(),
            [SaleItemDraft.New(first, 2, 10m), SaleItemDraft.New(duplicate, 2, 10m)],
            SaleTestData.Now);

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.SaleItem.DuplicateProduct);
    }

    [Fact]
    public void Create_ShouldAcceptDistinctProductsWithSameDescription()
    {
        var first = ExternalIdentity.Create("PRODUCT-001", "Same description");
        var second = ExternalIdentity.Create("PRODUCT-002", "Same description");

        var sale = Sale.Create(
            "SALE-0001",
            SaleTestData.Now,
            SaleTestData.Customer(),
            SaleTestData.Branch(),
            [SaleItemDraft.New(first, 1, 10m), SaleItemDraft.New(second, 1, 10m)],
            SaleTestData.Now);

        sale.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Create_ShouldTreatProductExternalIdsAsCaseSensitive()
    {
        var lowerCase = ExternalIdentity.Create("product-001", "Product");
        var upperCase = ExternalIdentity.Create("PRODUCT-001", "Product");

        var sale = Sale.Create(
            "SALE-0001",
            SaleTestData.Now,
            SaleTestData.Customer(),
            SaleTestData.Branch(),
            [SaleItemDraft.New(lowerCase, 1, 10m), SaleItemDraft.New(upperCase, 1, 10m)],
            SaleTestData.Now);

        sale.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Create_ShouldRejectEmptyItems()
    {
        var action = () => Sale.Create(
            "SALE-0001",
            SaleTestData.Now,
            SaleTestData.Customer(),
            SaleTestData.Branch(),
            [],
            SaleTestData.Now);

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.Sale.ItemsRequired);
    }

    [Fact]
    public void Create_ShouldRejectNullItemsCollection()
    {
        var action = () => Sale.Create(
            "SALE-0001",
            SaleTestData.Now,
            SaleTestData.Customer(),
            SaleTestData.Branch(),
            null,
            SaleTestData.Now);

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.Sale.ItemsRequired);
    }

    [Fact]
    public void Create_ShouldRejectNullItem()
    {
        var action = () => Sale.Create(
            "SALE-0001",
            SaleTestData.Now,
            SaleTestData.Customer(),
            SaleTestData.Branch(),
            new SaleItemDraft?[] { null },
            SaleTestData.Now);

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.SaleItem.NullItem);
    }

    [Fact]
    public void Create_ShouldRejectClientProvidedItemId()
    {
        var action = () => Sale.Create(
            "SALE-0001",
            SaleTestData.Now,
            SaleTestData.Customer(),
            SaleTestData.Branch(),
            [SaleItemDraft.Existing(Guid.NewGuid(), SaleTestData.Product(1), 1, 10m)],
            SaleTestData.Now);

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.SaleItem.IdNotAllowedOnCreate);
    }

    [Fact]
    public void Create_ShouldRejectSaleNumberAboveMaximumLength()
    {
        var action = () => Sale.Create(
            new string('A', Sale.SaleNumberMaximumLength + 1),
            SaleTestData.Now,
            SaleTestData.Customer(),
            SaleTestData.Branch(),
            [SaleTestData.NewItem(1)],
            SaleTestData.Now);

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.Sale.NumberTooLong);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldRejectBlankSaleNumber(string number)
    {
        var action = () => Sale.Create(
            number,
            SaleTestData.Now,
            SaleTestData.Customer(),
            SaleTestData.Branch(),
            [SaleTestData.NewItem(1)],
            SaleTestData.Now);

        action.Should().Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.Sale.NumberRequired);
    }

    [Fact]
    public void Create_ShouldRejectControlCharactersInSaleNumber()
    {
        var action = () => Sale.Create(
            "SALE\u0001NUMBER",
            SaleTestData.Now,
            SaleTestData.Customer(),
            SaleTestData.Branch(),
            [SaleTestData.NewItem(1)],
            SaleTestData.Now);

        action.Should().Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.Sale.NumberContainsControlCharacter);
    }

    [Fact]
    public void Create_ShouldRejectMoreThanOneHundredItems()
    {
        var items = Enumerable.Range(1, 101)
            .Select(number => SaleTestData.NewItem(number))
            .ToArray();

        var action = () => SaleTestData.CreateSale(items);

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.Sale.TooManyItems);
    }

    [Fact]
    public void Create_ShouldCalculateMaximumSupportedAggregateWithoutOverflow()
    {
        var items = Enumerable.Range(1, 100)
            .Select(number => SaleTestData.NewItem(number, 20, 1_000_000m))
            .ToArray();

        var sale = SaleTestData.CreateSale(items);

        sale.TotalAmount.Should().Be(1_600_000_000m);
    }

    [Fact]
    public void Create_ShouldAcceptSaleDateAtFutureToleranceBoundary()
    {
        var sale = Sale.Create(
            "SALE-0001",
            SaleTestData.Now.AddMinutes(5),
            SaleTestData.Customer(),
            SaleTestData.Branch(),
            [SaleTestData.NewItem(1)],
            SaleTestData.Now);

        sale.SaleDate.Should().Be(SaleTestData.Now.AddMinutes(5));
    }

    [Fact]
    public void Create_ShouldRejectSaleDateAfterFutureToleranceBoundary()
    {
        var action = () => Sale.Create(
            "SALE-0001",
            SaleTestData.Now.AddMinutes(5).AddTicks(1),
            SaleTestData.Customer(),
            SaleTestData.Branch(),
            [SaleTestData.NewItem(1)],
            SaleTestData.Now);

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.Sale.DateTooFarInFuture);
    }

    [Fact]
    public void Update_ShouldRecalculateItemsAddNewLineAndRaiseOneEvent()
    {
        var sale = SaleTestData.CreateSale(SaleTestData.NewItem(1, 3, 10m));
        var existing = sale.Items.Single();
        sale.ClearDomainEvents();

        var changed = sale.Update(
            sale.SaleDate,
            sale.Customer,
            sale.Branch,
            [
                SaleTestData.ExistingItem(existing, quantity: 4),
                SaleTestData.NewItem(2, 10, 10m)
            ],
            SaleTestData.Now.AddMinutes(1));

        changed.Should().BeTrue();
        sale.Items.Should().HaveCount(2);
        sale.Items[0].DiscountRate.Should().Be(0.10m);
        sale.Items[1].DiscountRate.Should().Be(0.20m);
        sale.TotalAmount.Should().Be(116m);
        sale.Version.Should().Be(2);
        sale.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SaleModifiedEvent>();
    }

    [Theory]
    [MemberData(nameof(DiscountTransitionCases))]
    public void Update_ShouldRecalculateDiscountWhenQuantityCrossesTier(
        int initialQuantity,
        int updatedQuantity,
        decimal expectedRate)
    {
        var sale = SaleTestData.CreateSale(SaleTestData.NewItem(1, initialQuantity, 10m));
        var item = sale.Items.Single();

        sale.Update(
            sale.SaleDate,
            sale.Customer,
            sale.Branch,
            [SaleTestData.ExistingItem(item, quantity: updatedQuantity)],
            SaleTestData.Now.AddMinutes(1));

        item.DiscountRate.Should().Be(expectedRate);
    }

    [Fact]
    public void Update_ShouldTreatIdenticalCandidateAsNoOp()
    {
        var sale = SaleTestData.CreateSale(SaleTestData.NewItem(1, 4, 10m));
        var item = sale.Items.Single();
        sale.ClearDomainEvents();

        var changed = sale.Update(
            sale.SaleDate,
            sale.Customer,
            sale.Branch,
            [SaleTestData.ExistingItem(item)],
            SaleTestData.Now.AddMinutes(1));

        changed.Should().BeFalse();
        sale.Version.Should().Be(1);
        sale.UpdatedAt.Should().Be(SaleTestData.Now);
        sale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_ShouldPreserveEntireStateWhenLastCandidateIsInvalid()
    {
        var sale = SaleTestData.CreateSale(
            SaleTestData.NewItem(1, 3, 10m),
            SaleTestData.NewItem(2, 4, 10m));
        var first = sale.Items[0];
        var second = sale.Items[1];
        var totalBefore = sale.TotalAmount;
        sale.ClearDomainEvents();

        var action = () => sale.Update(
            sale.SaleDate,
            sale.Customer,
            sale.Branch,
            [
                SaleTestData.ExistingItem(first, quantity: 10),
                SaleTestData.ExistingItem(second, unitPrice: 1.001m)
            ],
            SaleTestData.Now.AddMinutes(1));

        action.Should().Throw<DomainValidationException>();
        first.Quantity.Should().Be(3);
        second.UnitPrice.Should().Be(10m);
        sale.TotalAmount.Should().Be(totalBefore);
        sale.Version.Should().Be(1);
        sale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_ShouldRejectOmittedActiveItemWithoutMutation()
    {
        var sale = SaleTestData.CreateSale(
            SaleTestData.NewItem(1),
            SaleTestData.NewItem(2));
        var totalBefore = sale.TotalAmount;
        sale.ClearDomainEvents();

        var action = () => sale.Update(
            sale.SaleDate,
            sale.Customer,
            sale.Branch,
            [SaleTestData.ExistingItem(sale.Items[0], quantity: 4)],
            SaleTestData.Now.AddMinutes(1));

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.SaleItem.ActiveItemOmitted);
        sale.Items[0].Quantity.Should().Be(1);
        sale.TotalAmount.Should().Be(totalBefore);
        sale.Version.Should().Be(1);
    }

    [Fact]
    public void Update_ShouldRejectChangingExistingProductSnapshot()
    {
        var sale = SaleTestData.CreateSale(SaleTestData.NewItem(1));
        var item = sale.Items.Single();

        var action = () => sale.Update(
            sale.SaleDate,
            sale.Customer,
            sale.Branch,
            [SaleItemDraft.Existing(
                item.Id,
                ExternalIdentity.Create(item.Product.ExternalId, "Changed description"),
                item.Quantity,
                item.UnitPrice)],
            SaleTestData.Now.AddMinutes(1));

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.SaleItem.ProductIsImmutable);
    }

    [Fact]
    public void Update_ShouldRejectEmptyUnknownCancelledAndRepeatedItemIdentifiers()
    {
        var sale = SaleTestData.CreateSale(SaleTestData.NewItem(1), SaleTestData.NewItem(2));
        var first = sale.Items[0];
        var second = sale.Items[1];
        sale.CancelItem(second.Id, SaleTestData.Now.AddMinutes(1));

        foreach (var invalidId in new[] { Guid.Empty, Guid.NewGuid(), second.Id })
        {
            var action = () => sale.Update(
                sale.SaleDate,
                sale.Customer,
                sale.Branch,
                [SaleItemDraft.Existing(invalidId, first.Product, 1, 10m)],
                SaleTestData.Now.AddMinutes(2));
            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be(DomainErrorCodes.SaleItem.InvalidId);
        }

        var duplicate = () => sale.Update(
            sale.SaleDate,
            sale.Customer,
            sale.Branch,
            [
                SaleTestData.ExistingItem(first),
                SaleItemDraft.Existing(first.Id, SaleTestData.Product(3), first.Quantity, first.UnitPrice)
            ],
            SaleTestData.Now.AddMinutes(2));
        duplicate.Should().Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.SaleItem.DuplicateId);
    }

    [Fact]
    public void Update_ShouldCountCancelledHistoryAgainstItemCapacity()
    {
        var sale = SaleTestData.CreateSale(
            Enumerable.Range(1, Sale.MaximumItems)
                .Select(index => SaleTestData.NewItem(index))
                .ToArray());
        sale.CancelItem(sale.Items[0].Id, SaleTestData.Now.AddMinutes(1));
        var active = sale.Items.Where(item => !item.IsCancelled)
            .Select(item => SaleTestData.ExistingItem(item))
            .Append(SaleTestData.NewItem(101))
            .ToArray();

        var action = () => sale.Update(
            sale.SaleDate,
            sale.Customer,
            sale.Branch,
            active,
            SaleTestData.Now.AddMinutes(2));

        action.Should().Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.Sale.TooManyItems);
    }

    [Fact]
    public void Operations_ShouldRejectATimestampBeforeCreation()
    {
        var sale = SaleTestData.CreateSale(SaleTestData.NewItem(1));
        var action = () => sale.Cancel(SaleTestData.Now.AddTicks(-1));
        action.Should().Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.Sale.OperationBeforeCreation);
    }

    [Fact]
    public void CancelItem_ShouldPreserveHistoryAndRemoveLineFromEffectiveTotal()
    {
        var sale = SaleTestData.CreateSale(
            SaleTestData.NewItem(1, 4, 10m),
            SaleTestData.NewItem(2, 1, 10m));
        var item = sale.Items[0];
        var historicalTotal = item.TotalAmount;
        sale.ClearDomainEvents();

        var changed = sale.CancelItem(item.Id, SaleTestData.Now.AddMinutes(1));

        changed.Should().BeTrue();
        item.IsCancelled.Should().BeTrue();
        item.TotalAmount.Should().Be(historicalTotal);
        item.EffectiveAmount.Should().Be(0m);
        sale.TotalAmount.Should().Be(10m);
        sale.IsCancelled.Should().BeFalse();
        sale.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ItemCancelledEvent>();
    }

    [Fact]
    public void CancelItem_ShouldCancelSaleWhenLastActiveItemIsCancelled()
    {
        var sale = SaleTestData.CreateSale(SaleTestData.NewItem(1, 4, 10m));
        var item = sale.Items.Single();
        sale.ClearDomainEvents();

        sale.CancelItem(item.Id, SaleTestData.Now.AddMinutes(1));

        sale.IsCancelled.Should().BeTrue();
        sale.TotalAmount.Should().Be(0m);
        sale.Version.Should().Be(2);
        sale.DomainEvents.Should().HaveCount(2);
        sale.DomainEvents[0].Should().BeOfType<ItemCancelledEvent>();
        sale.DomainEvents[1].Should().BeOfType<SaleCancelledEvent>();
    }

    [Fact]
    public void CancelItem_ShouldBeIdempotentForAlreadyCancelledItem()
    {
        var sale = SaleTestData.CreateSale(
            SaleTestData.NewItem(1),
            SaleTestData.NewItem(2));
        var item = sale.Items[0];
        sale.CancelItem(item.Id, SaleTestData.Now.AddMinutes(1));
        var version = sale.Version;
        sale.ClearDomainEvents();

        var changed = sale.CancelItem(item.Id, SaleTestData.Now.AddMinutes(2));

        changed.Should().BeFalse();
        sale.Version.Should().Be(version);
        sale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Cancel_ShouldCancelAllActiveItemsAndRaiseOnlySaleEvent()
    {
        var sale = SaleTestData.CreateSale(
            SaleTestData.NewItem(1),
            SaleTestData.NewItem(2));
        sale.ClearDomainEvents();

        var changed = sale.Cancel(SaleTestData.Now.AddMinutes(1));

        changed.Should().BeTrue();
        sale.Items.Should().OnlyContain(item => item.IsCancelled);
        sale.TotalAmount.Should().Be(0m);
        sale.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SaleCancelledEvent>();
    }

    [Fact]
    public void Cancel_ShouldRaiseOnlySaleEventWhenOneItemWasAlreadyCancelled()
    {
        var sale = SaleTestData.CreateSale(
            SaleTestData.NewItem(1),
            SaleTestData.NewItem(2));
        sale.CancelItem(sale.Items[0].Id, SaleTestData.Now.AddMinutes(1));
        sale.ClearDomainEvents();

        sale.Cancel(SaleTestData.Now.AddMinutes(2));

        sale.Items.Should().OnlyContain(item => item.IsCancelled);
        sale.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SaleCancelledEvent>();
    }

    [Fact]
    public void Cancel_ShouldBeIdempotentForAlreadyCancelledSale()
    {
        var sale = SaleTestData.CreateSale(SaleTestData.NewItem(1));
        sale.Cancel(SaleTestData.Now.AddMinutes(1));
        var version = sale.Version;
        sale.ClearDomainEvents();

        var changed = sale.Cancel(SaleTestData.Now.AddMinutes(2));

        changed.Should().BeFalse();
        sale.Version.Should().Be(version);
        sale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_ShouldRejectCancelledSale()
    {
        var sale = SaleTestData.CreateSale(SaleTestData.NewItem(1));
        var item = sale.Items.Single();
        sale.Cancel(SaleTestData.Now.AddMinutes(1));

        var action = () => sale.Update(
            sale.SaleDate,
            sale.Customer,
            sale.Branch,
            [SaleTestData.ExistingItem(item)],
            SaleTestData.Now.AddMinutes(2));

        action.Should()
            .Throw<DomainConflictException>()
            .Which.Code.Should().Be(DomainErrorCodes.Sale.Cancelled);
    }

    [Fact]
    public void Update_ShouldRejectReintroducingCancelledProduct()
    {
        var sale = SaleTestData.CreateSale(
            SaleTestData.NewItem(1),
            SaleTestData.NewItem(2));
        var cancelled = sale.Items[0];
        var active = sale.Items[1];
        sale.CancelItem(cancelled.Id, SaleTestData.Now.AddMinutes(1));

        var action = () => sale.Update(
            sale.SaleDate,
            sale.Customer,
            sale.Branch,
            [
                SaleTestData.ExistingItem(active),
                SaleItemDraft.New(cancelled.Product, 1, 10m)
            ],
            SaleTestData.Now.AddMinutes(2));

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.SaleItem.DuplicateProduct);
    }

    [Fact]
    public void Delete_ShouldSoftDeleteAndPreventFurtherChanges()
    {
        var sale = SaleTestData.CreateSale(SaleTestData.NewItem(1));
        var totalBefore = sale.TotalAmount;
        sale.ClearDomainEvents();

        var changed = sale.Delete(SaleTestData.Now.AddMinutes(1));

        changed.Should().BeTrue();
        sale.IsDeleted.Should().BeTrue();
        sale.DeletedAt.Should().Be(SaleTestData.Now.AddMinutes(1));
        sale.TotalAmount.Should().Be(totalBefore);
        sale.Version.Should().Be(2);
        sale.DomainEvents.Should().BeEmpty();

        var action = () => sale.Cancel(SaleTestData.Now.AddMinutes(2));
        action.Should()
            .Throw<DomainConflictException>()
            .Which.Code.Should().Be(DomainErrorCodes.Sale.Deleted);
    }

    [Fact]
    public void Delete_ShouldBeIdempotent()
    {
        var sale = SaleTestData.CreateSale(SaleTestData.NewItem(1));
        sale.Delete(SaleTestData.Now.AddMinutes(1));
        var version = sale.Version;

        var changed = sale.Delete(SaleTestData.Now.AddMinutes(2));

        changed.Should().BeFalse();
        sale.Version.Should().Be(version);
        sale.DeletedAt.Should().Be(SaleTestData.Now.AddMinutes(1));
    }

    [Fact]
    public void CancelItem_ShouldRejectUnknownItemWithoutMutation()
    {
        var sale = SaleTestData.CreateSale(SaleTestData.NewItem(1));
        var totalBefore = sale.TotalAmount;
        sale.ClearDomainEvents();

        var action = () => sale.CancelItem(Guid.NewGuid(), SaleTestData.Now.AddMinutes(1));

        action.Should()
            .Throw<DomainNotFoundException>()
            .Which.Code.Should().Be(DomainErrorCodes.SaleItem.NotFound);
        sale.TotalAmount.Should().Be(totalBefore);
        sale.Version.Should().Be(1);
        sale.DomainEvents.Should().BeEmpty();
    }
}
