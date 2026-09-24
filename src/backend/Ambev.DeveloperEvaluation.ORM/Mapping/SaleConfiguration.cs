using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambev.DeveloperEvaluation.ORM.Mapping;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales", table =>
        {
            table.HasCheckConstraint("CK_Sales_TotalAmount_NonNegative", "\"TotalAmount\" >= 0");
            table.HasCheckConstraint("CK_Sales_Version_Positive", "\"Version\" > 0");
            table.HasCheckConstraint(
                "CK_Sales_Cancellation_State",
                "(\"IsCancelled\" = FALSE AND \"CancelledAt\" IS NULL) OR " +
                "(\"IsCancelled\" = TRUE AND \"CancelledAt\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Sales_Deletion_State",
                "(\"IsDeleted\" = FALSE AND \"DeletedAt\" IS NULL) OR " +
                "(\"IsDeleted\" = TRUE AND \"DeletedAt\" IS NOT NULL)");
        });

        builder.HasKey(sale => sale.Id);
        builder.Property(sale => sale.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(sale => sale.SaleNumber)
            .IsRequired()
            .HasMaxLength(Sale.SaleNumberMaximumLength);

        builder.Property(sale => sale.SaleDate)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        ConfigureIdentity(
            builder.OwnsOne(sale => sale.Customer),
            "Customer",
            "IX_Sales_CustomerExternalId");
        builder.Navigation(sale => sale.Customer).IsRequired();

        ConfigureIdentity(
            builder.OwnsOne(sale => sale.Branch),
            "Branch",
            "IX_Sales_BranchExternalId");
        builder.Navigation(sale => sale.Branch).IsRequired();

        builder.Property(sale => sale.TotalAmount)
            .IsRequired()
            .HasPrecision(18, 2);
        builder.Property(sale => sale.IsCancelled).IsRequired();
        builder.Property(sale => sale.CancelledAt).HasColumnType("timestamp with time zone");
        builder.Property(sale => sale.IsDeleted).IsRequired();
        builder.Property(sale => sale.DeletedAt).HasColumnType("timestamp with time zone");
        builder.Property(sale => sale.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");
        builder.Property(sale => sale.UpdatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");
        builder.Property(sale => sale.Version)
            .IsRequired()
            .IsConcurrencyToken();

        builder.HasIndex(sale => sale.SaleNumber)
            .IsUnique()
            .HasDatabaseName("UX_Sales_SaleNumber");
        builder.HasIndex(sale => new { sale.SaleDate, sale.Id })
            .IsDescending(true, false)
            .HasDatabaseName("IX_Sales_SaleDate_Id");
        builder.HasIndex(sale => new { sale.IsDeleted, sale.IsCancelled })
            .HasDatabaseName("IX_Sales_Deletion_Cancellation");

        builder.HasMany(sale => sale.Items)
            .WithOne()
            .HasForeignKey(item => item.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(sale => sale.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(sale => sale.DomainEvents);
        builder.HasQueryFilter(sale => !sale.IsDeleted);
    }

    private static void ConfigureIdentity(
        OwnedNavigationBuilder<Sale, ExternalIdentity> identity,
        string columnPrefix,
        string indexName)
    {
        identity.Property(value => value.ExternalId)
            .HasColumnName($"{columnPrefix}ExternalId")
            .IsRequired()
            .HasMaxLength(ExternalIdentity.ExternalIdMaximumLength);
        identity.Property(value => value.Name)
            .HasColumnName($"{columnPrefix}Name")
            .IsRequired()
            .HasMaxLength(ExternalIdentity.NameMaximumLength);
        identity.HasIndex(value => value.ExternalId)
            .HasDatabaseName(indexName);
    }
}
