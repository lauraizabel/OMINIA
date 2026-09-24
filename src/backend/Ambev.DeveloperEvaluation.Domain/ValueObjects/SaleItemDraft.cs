namespace Ambev.DeveloperEvaluation.Domain.ValueObjects;

public sealed record SaleItemDraft(
    Guid? Id,
    ExternalIdentity Product,
    int Quantity,
    decimal UnitPrice)
{
    public static SaleItemDraft New(ExternalIdentity product, int quantity, decimal unitPrice)
    {
        return new SaleItemDraft(null, product, quantity, unitPrice);
    }

    public static SaleItemDraft Existing(
        Guid id,
        ExternalIdentity product,
        int quantity,
        decimal unitPrice)
    {
        return new SaleItemDraft(id, product, quantity, unitPrice);
    }
}
