namespace Ambev.DeveloperEvaluation.Domain.ValueObjects;

public readonly record struct SaleAmounts(
    decimal DiscountRate,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal TotalAmount);
