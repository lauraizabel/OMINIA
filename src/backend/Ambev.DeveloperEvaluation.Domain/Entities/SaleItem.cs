using Ambev.DeveloperEvaluation.Domain.Services;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;

namespace Ambev.DeveloperEvaluation.Domain.Entities;

public sealed class SaleItem
{
    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public ExternalIdentity Product { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountRate { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal EffectiveAmount => IsCancelled ? 0m : TotalAmount;
    public bool IsCancelled { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    private SaleItem()
    {
    }

    private SaleItem(
        Guid id,
        Guid saleId,
        ExternalIdentity product,
        int quantity,
        decimal unitPrice,
        SaleAmounts amounts)
    {
        Id = id;
        SaleId = saleId;
        Product = product;
        Apply(quantity, unitPrice, amounts);
    }

    internal static SaleItem Create(
        Guid saleId,
        ExternalIdentity product,
        int quantity,
        decimal unitPrice)
    {
        ArgumentNullException.ThrowIfNull(product);
        var amounts = SaleDiscountPolicy.Calculate(quantity, unitPrice);

        return new SaleItem(
            Guid.NewGuid(),
            saleId,
            product,
            quantity,
            unitPrice,
            amounts);
    }

    internal void Update(int quantity, decimal unitPrice)
    {
        var amounts = SaleDiscountPolicy.Calculate(quantity, unitPrice);
        Apply(quantity, unitPrice, amounts);
    }

    internal void Cancel(DateTimeOffset cancelledAt)
    {
        if (IsCancelled)
            return;

        IsCancelled = true;
        CancelledAt = cancelledAt;
    }

    private void Apply(int quantity, decimal unitPrice, SaleAmounts amounts)
    {
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountRate = amounts.DiscountRate;
        GrossAmount = amounts.GrossAmount;
        DiscountAmount = amounts.DiscountAmount;
        TotalAmount = amounts.TotalAmount;
    }
}
