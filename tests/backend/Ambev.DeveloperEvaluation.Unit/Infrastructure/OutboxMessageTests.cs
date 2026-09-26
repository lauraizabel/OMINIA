using System.Text.Json;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Infrastructure;

public sealed class OutboxMessageTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 26, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void From_preserves_event_identity_metadata_and_payload()
    {
        var saleId = Guid.NewGuid();
        var domainEvent = new ItemCancelledEvent(saleId, Guid.NewGuid(), 4, Now);

        var message = OutboxMessage.From(domainEvent, 1, "correlation-id", Now.AddSeconds(1));

        message.Id.Should().Be(domainEvent.EventId);
        message.AggregateId.Should().Be(saleId);
        message.AggregateVersion.Should().Be(4);
        message.Sequence.Should().Be(1);
        message.EventType.Should().Be(nameof(ItemCancelledEvent));
        message.CorrelationId.Should().Be("correlation-id");
        JsonDocument.Parse(message.Payload).RootElement.GetProperty("ItemId").GetGuid()
            .Should().Be(domainEvent.ItemId);
        message.ToEventMessage().EventId.Should().Be(domainEvent.EventId);
    }

    [Fact]
    public void Complete_requires_the_current_lease_owner()
    {
        var message = Message();
        var unclaimedAction = () => message.Complete(Guid.Empty, Now);
        unclaimedAction.Should().Throw<InvalidOperationException>();
        var owner = Guid.NewGuid();
        message.Claim(owner, Now.AddMinutes(1));

        var action = () => message.Complete(Guid.NewGuid(), Now);

        action.Should().Throw<InvalidOperationException>();
        message.Complete(owner, Now);
        message.ProcessedAt.Should().Be(Now);
        message.LockToken.Should().BeNull();
    }

    [Fact]
    public void Retry_releases_the_lease_and_records_the_failure()
    {
        var message = Message();
        var owner = Guid.NewGuid();
        message.Claim(owner, Now.AddMinutes(1));

        message.Retry(owner, Now.AddSeconds(10), "temporary failure");

        message.AttemptCount.Should().Be(1);
        message.NextAttemptAt.Should().Be(Now.AddSeconds(10));
        message.LastError.Should().Be("temporary failure");
        message.LockToken.Should().BeNull();
    }

    [Fact]
    public void Dead_letter_can_be_replayed_but_an_active_message_cannot()
    {
        var message = Message();
        var owner = Guid.NewGuid();
        var invalidReplay = () => message.Replay(Now);
        invalidReplay.Should().Throw<InvalidOperationException>();

        message.Claim(owner, Now.AddMinutes(1));
        message.DeadLetter(owner, Now, "permanent failure");
        message.Replay(Now.AddMinutes(2));

        message.FailedAt.Should().BeNull();
        message.LastError.Should().BeNull();
        message.AttemptCount.Should().Be(0);
        message.NextAttemptAt.Should().Be(Now.AddMinutes(2));
    }

    private static OutboxMessage Message() => OutboxMessage.From(
        new SaleCreatedEvent(Guid.NewGuid(), 1, Now),
        0,
        "test",
        Now);
}
