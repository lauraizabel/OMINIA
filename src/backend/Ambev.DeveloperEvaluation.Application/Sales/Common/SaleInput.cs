using Ambev.DeveloperEvaluation.Domain.ValueObjects;

namespace Ambev.DeveloperEvaluation.Application.Sales.Common;

public sealed record ExternalIdentityInput(string ExternalId, string Name)
{
    public ExternalIdentity ToDomain() => ExternalIdentity.Create(ExternalId, Name);
}

public sealed record SaleItemInput(
    Guid? Id,
    ExternalIdentityInput Product,
    int Quantity,
    decimal UnitPrice)
{
    public SaleItemDraft ToDomain()
    {
        var product = Product.ToDomain();
        return Id.HasValue
            ? SaleItemDraft.Existing(Id.Value, product, Quantity, UnitPrice)
            : SaleItemDraft.New(product, Quantity, UnitPrice);
    }
}
