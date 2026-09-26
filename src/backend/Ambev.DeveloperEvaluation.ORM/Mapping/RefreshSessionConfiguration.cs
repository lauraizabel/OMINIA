using Ambev.DeveloperEvaluation.ORM.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambev.DeveloperEvaluation.ORM.Mapping;

public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.ToTable("RefreshSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(session => session.RevocationReason).HasMaxLength(50);

        builder.HasIndex(session => session.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_RefreshSessions_TokenHash");
        builder.HasIndex(session => new { session.FamilyId, session.RevokedAt })
            .HasDatabaseName("IX_RefreshSessions_Family_Active");
        builder.HasIndex(session => session.AbsoluteExpiresAt)
            .HasDatabaseName("IX_RefreshSessions_Expiration");

        builder.HasOne(session => session.User)
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
