using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Services;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambev.DeveloperEvaluation.ORM.Mapping;

public sealed class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems", table =>
        {
            table.HasCheckConstraint(
                "CK_SaleItems_Quantity_Range",
                $"\"Quantity\" BETWEEN {SaleDiscountPolicy.MinimumQuantity} AND {SaleDiscountPolicy.MaximumQuantity}");
            table.HasCheckConstraint(
                "CK_SaleItems_UnitPrice_Range",
                $"\"UnitPrice\" > 0 AND \"UnitPrice\" <= {SaleDiscountPolicy.MaximumUnitPrice}");
            table.HasCheckConstraint(
                "CK_SaleItems_DiscountRate_Range",
                "\"DiscountRate\" >= 0 AND \"DiscountRate\" <= 1");
            table.HasCheckConstraint(
                "CK_SaleItems_Monetary_Amounts",
                "\"GrossAmount\" >= 0 AND \"DiscountAmount\" >= 0 AND " +
                "\"DiscountAmount\" <= \"GrossAmount\" AND " +
                "\"TotalAmount\" = \"GrossAmount\" - \"DiscountAmount\"");
            table.HasCheckConstraint(
                "CK_SaleItems_Cancellation_State",
                "(\"IsCancelled\" = FALSE AND \"CancelledAt\" IS NULL) OR " +
                "(\"IsCancelled\" = TRUE AND \"CancelledAt\" IS NOT NULL)");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();
        builder.Property(item => item.SaleId)
            .IsRequired()
            .HasColumnType("uuid");

        var product = builder.OwnsOne(item => item.Product);
        product.Property(identity => identity.ExternalId)
            .HasColumnName("ProductExternalId")
            .IsRequired()
            .HasMaxLength(ExternalIdentity.ExternalIdMaximumLength);
        product.Property(identity => identity.Name)
            .HasColumnName("ProductName")
            .IsRequired()
            .HasMaxLength(ExternalIdentity.NameMaximumLength);
        builder.Navigation(item => item.Product).IsRequired();

        builder.Property(item => item.Quantity).IsRequired();
        builder.Property(item => item.UnitPrice)
            .IsRequired()
            .HasPrecision(18, 2);
        builder.Property(item => item.DiscountRate)
            .IsRequired()
            .HasPrecision(5, 4);
        builder.Property(item => item.GrossAmount)
            .IsRequired()
            .HasPrecision(18, 2);
        builder.Property(item => item.DiscountAmount)
            .IsRequired()
            .HasPrecision(18, 2);
        builder.Property(item => item.TotalAmount)
            .IsRequired()
            .HasPrecision(18, 2);
        builder.Property(item => item.IsCancelled).IsRequired();
        builder.Property(item => item.CancelledAt).HasColumnType("timestamp with time zone");

        builder.Ignore(item => item.EffectiveAmount);
    }
}
