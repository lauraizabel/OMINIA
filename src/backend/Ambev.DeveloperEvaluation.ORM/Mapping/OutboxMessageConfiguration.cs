using Ambev.DeveloperEvaluation.ORM.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambev.DeveloperEvaluation.ORM.Mapping;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.EventType).HasMaxLength(100).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(2000);
        builder.Property(message => message.LockToken).IsConcurrencyToken();

        builder.HasIndex(message => new
        {
            message.ProcessedAt,
            message.FailedAt,
            message.NextAttemptAt,
            message.LockedUntil,
            message.OccurredAt
        })
            .HasDatabaseName("IX_OutboxMessages_Dispatch")
            .HasFilter("\"ProcessedAt\" IS NULL AND \"FailedAt\" IS NULL");

        builder.HasIndex(message => new
        {
            message.AggregateId,
            message.AggregateVersion,
            message.Sequence
        })
            .HasDatabaseName("IX_OutboxMessages_AggregateOrder");

        builder.HasIndex(message => message.ProcessedAt)
            .HasDatabaseName("IX_OutboxMessages_ProcessedRetention")
            .HasFilter("\"ProcessedAt\" IS NOT NULL");
    }
}
