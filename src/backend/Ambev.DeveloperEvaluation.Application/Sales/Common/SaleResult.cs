using Ambev.DeveloperEvaluation.Domain.Entities;

namespace Ambev.DeveloperEvaluation.Application.Sales.Common;

public sealed record ExternalIdentityResult(string ExternalId, string Name);

public sealed record SaleItemResult(
    Guid Id,
    ExternalIdentityResult Product,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountRate,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal EffectiveAmount,
    bool IsCancelled,
    DateTimeOffset? CancelledAt);

public sealed record SaleResult(
    Guid Id,
    string SaleNumber,
    DateTimeOffset SaleDate,
    ExternalIdentityResult Customer,
    ExternalIdentityResult Branch,
    IReadOnlyCollection<SaleItemResult> Items,
    decimal TotalAmount,
    bool IsCancelled,
    DateTimeOffset? CancelledAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Version)
{
    public static SaleResult From(Sale sale) => new(
        sale.Id,
        sale.SaleNumber,
        sale.SaleDate,
        new ExternalIdentityResult(sale.Customer.ExternalId, sale.Customer.Name),
        new ExternalIdentityResult(sale.Branch.ExternalId, sale.Branch.Name),
        sale.Items.Select(item => new SaleItemResult(
            item.Id,
            new ExternalIdentityResult(item.Product.ExternalId, item.Product.Name),
            item.Quantity,
            item.UnitPrice,
            item.DiscountRate,
            item.GrossAmount,
            item.DiscountAmount,
            item.TotalAmount,
            item.EffectiveAmount,
            item.IsCancelled,
            item.CancelledAt)).ToArray(),
        sale.TotalAmount,
        sale.IsCancelled,
        sale.CancelledAt,
        sale.CreatedAt,
        sale.UpdatedAt,
        sale.Version);
}
