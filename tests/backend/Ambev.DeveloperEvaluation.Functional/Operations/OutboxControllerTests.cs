using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.WebApi.Features.Operations;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Operations;

public sealed class OutboxControllerTests
{
    private readonly IOutboxAdministration _administration = Substitute.For<IOutboxAdministration>();

    [Fact]
    public async Task Status_returns_operational_counts_without_event_payloads()
    {
        var failedAt = new DateTimeOffset(2026, 9, 26, 1, 0, 0, TimeSpan.Zero);
        var deadLetter = new DeadLetterEvent(
            Guid.NewGuid(),
            "SaleModifiedEvent",
            Guid.NewGuid(),
            3,
            10,
            failedAt,
            "Audit storage unavailable");
        var status = new OutboxStatus(4, 1, failedAt.AddMinutes(-2), [deadLetter]);
        _administration.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(status);
        var controller = new OutboxController(_administration);

        var response = await controller.GetStatus(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(status, ok.Value);
    }

    [Fact]
    public async Task Replay_returns_accepted_only_for_a_dead_letter_event()
    {
        var eventId = Guid.NewGuid();
        _administration.ReplayAsync(eventId, Arg.Any<CancellationToken>()).Returns(true);
        var controller = new OutboxController(_administration);

        var response = await controller.Replay(eventId, CancellationToken.None);

        Assert.IsType<AcceptedResult>(response);
    }

    [Fact]
    public async Task Replay_returns_not_found_for_an_unknown_or_active_event()
    {
        var eventId = Guid.NewGuid();
        _administration.ReplayAsync(eventId, Arg.Any<CancellationToken>()).Returns(false);
        var controller = new OutboxController(_administration);

        var response = await controller.Replay(eventId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(response);
    }
}
