using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Sales;

public sealed class SaleDiscountPolicyTests
{
    public static TheoryData<int, decimal, decimal, decimal, decimal, decimal> DiscountCases => new()
    {
        { 1, 10m, 0m, 10m, 0m, 10m },
        { 3, 10m, 0m, 30m, 0m, 30m },
        { 4, 10m, 0.10m, 40m, 4m, 36m },
        { 9, 10m, 0.10m, 90m, 9m, 81m },
        { 10, 10m, 0.20m, 100m, 20m, 80m },
        { 20, 10m, 0.20m, 200m, 40m, 160m }
    };

    public static TheoryData<int> InvalidQuantities => new()
    {
        -1,
        0,
        21,
        int.MaxValue
    };

    public static TheoryData<decimal> InvalidPrices => new()
    {
        -1m,
        0m,
        1.230m,
        1.001m,
        1_000_000.01m
    };

    public static TheoryData<int, decimal, decimal, decimal, decimal> CentCases => new()
    {
        { 4, 0.01m, 0.04m, 0m, 0.04m },
        { 10, 0.01m, 0.10m, 0.02m, 0.08m }
    };

    [Theory]
    [MemberData(nameof(DiscountCases))]
    public void Calculate_ShouldApplyExpectedTier(
        int quantity,
        decimal unitPrice,
        decimal expectedRate,
        decimal expectedGross,
        decimal expectedDiscount,
        decimal expectedTotal)
    {
        var result = SaleDiscountPolicy.Calculate(quantity, unitPrice);

        result.DiscountRate.Should().Be(expectedRate);
        result.GrossAmount.Should().Be(expectedGross);
        result.DiscountAmount.Should().Be(expectedDiscount);
        result.TotalAmount.Should().Be(expectedTotal);
    }

    [Theory]
    [MemberData(nameof(InvalidQuantities))]
    public void Calculate_ShouldRejectInvalidQuantity(int quantity)
    {
        var action = () => SaleDiscountPolicy.Calculate(quantity, 10m);

        action.Should()
            .Throw<DomainValidationException>()
            .Which.Code.Should().Be(DomainErrorCodes.SaleItem.QuantityOutOfRange);
    }

    [Theory]
    [MemberData(nameof(InvalidPrices))]
    public void Calculate_ShouldRejectInvalidUnitPrice(decimal unitPrice)
    {
        var action = () => SaleDiscountPolicy.Calculate(1, unitPrice);

        action.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Calculate_ShouldRoundLineDiscountAwayFromZero()
    {
        var result = SaleDiscountPolicy.Calculate(9, 0.05m);

        result.GrossAmount.Should().Be(0.45m);
        result.DiscountAmount.Should().Be(0.05m);
        result.TotalAmount.Should().Be(0.40m);
    }

    [Theory]
    [MemberData(nameof(CentCases))]
    public void Calculate_ShouldKeepCentValuesExact(
        int quantity,
        decimal unitPrice,
        decimal expectedGross,
        decimal expectedDiscount,
        decimal expectedTotal)
    {
        var result = SaleDiscountPolicy.Calculate(quantity, unitPrice);

        result.GrossAmount.Should().Be(expectedGross);
        result.DiscountAmount.Should().Be(expectedDiscount);
        result.TotalAmount.Should().Be(expectedTotal);
    }

    [Fact]
    public void Calculate_ShouldPreserveMonetaryInvariantsAcrossValidDomain()
    {
        decimal[] prices = [0.01m, 0.05m, 1.99m, 999_999.99m, 1_000_000m];

        foreach (var quantity in Enumerable.Range(1, 20))
        {
            foreach (var price in prices)
            {
                var result = SaleDiscountPolicy.Calculate(quantity, price);

                result.TotalAmount.Should().Be(result.GrossAmount - result.DiscountAmount);
                result.DiscountAmount.Should().BeGreaterThanOrEqualTo(0m);
                result.DiscountAmount.Should().BeLessThanOrEqualTo(result.GrossAmount);
            }
        }
    }
}
