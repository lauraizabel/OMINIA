using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;

namespace Ambev.DeveloperEvaluation.Domain.Services;

public static class SaleDiscountPolicy
{
    public const int MinimumQuantity = 1;
    public const int MaximumQuantity = 20;
    public const decimal MaximumUnitPrice = 1_000_000m;

    public static SaleAmounts Calculate(int quantity, decimal unitPrice)
    {
        ValidateQuantity(quantity);
        ValidateUnitPrice(unitPrice);

        var discountRate = quantity switch
        {
            >= 10 => 0.20m,
            >= 4 => 0.10m,
            _ => 0m
        };

        var grossAmount = quantity * unitPrice;
        var discountAmount = decimal.Round(
            grossAmount * discountRate,
            2,
            MidpointRounding.AwayFromZero);

        return new SaleAmounts(
            discountRate,
            grossAmount,
            discountAmount,
            grossAmount - discountAmount);
    }

    public static void ValidateQuantity(int quantity)
    {
        if (quantity is < MinimumQuantity or > MaximumQuantity)
            throw new DomainValidationException(
                DomainErrorCodes.SaleItem.QuantityOutOfRange,
                $"Quantity must be between {MinimumQuantity} and {MaximumQuantity}.");
    }

    public static void ValidateUnitPrice(decimal unitPrice)
    {
        if (unitPrice <= 0 || unitPrice > MaximumUnitPrice)
            throw new DomainValidationException(
                DomainErrorCodes.SaleItem.UnitPriceOutOfRange,
                $"Unit price must be greater than zero and at most {MaximumUnitPrice:F2}.");

        var scale = (decimal.GetBits(unitPrice)[3] >> 16) & 0x7F;
        if (scale > 2)
            throw new DomainValidationException(
                DomainErrorCodes.SaleItem.UnitPriceScaleExceeded,
                "Unit price must contain at most two decimal places.");
    }
}
