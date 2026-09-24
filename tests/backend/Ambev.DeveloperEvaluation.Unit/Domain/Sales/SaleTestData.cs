using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Sales;

internal static class SaleTestData
{
    internal static readonly DateTimeOffset Now =
        new(2026, 9, 23, 18, 0, 0, TimeSpan.Zero);

    internal static ExternalIdentity Customer(string id = "CUSTOMER-001", string name = "Example Customer")
    {
        return ExternalIdentity.Create(id, name);
    }

    internal static ExternalIdentity Branch(string id = "BRANCH-001", string name = "Fortaleza Branch")
    {
        return ExternalIdentity.Create(id, name);
    }

    internal static ExternalIdentity Product(int number)
    {
        return ExternalIdentity.Create($"PRODUCT-{number:000}", $"Product {number:000}");
    }

    internal static SaleItemDraft NewItem(int productNumber, int quantity = 1, decimal unitPrice = 10m)
    {
        return SaleItemDraft.New(Product(productNumber), quantity, unitPrice);
    }

    internal static Sale CreateSale(params SaleItemDraft[] items)
    {
        return Sale.Create(
            "SALE-0001",
            Now.AddHours(-1),
            Customer(),
            Branch(),
            items,
            Now);
    }

    internal static SaleItemDraft ExistingItem(SaleItem item, int? quantity = null, decimal? unitPrice = null)
    {
        return SaleItemDraft.Existing(
            item.Id,
            item.Product,
            quantity ?? item.Quantity,
            unitPrice ?? item.UnitPrice);
    }
}
