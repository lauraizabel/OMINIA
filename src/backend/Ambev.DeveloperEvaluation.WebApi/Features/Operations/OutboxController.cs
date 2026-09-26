using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Operations;

[ApiController]
[Route("api/operations/outbox")]
[Authorize(Policy = ApiPolicies.Administrators)]
public sealed class OutboxController : ControllerBase
{
    private readonly IOutboxAdministration _administration;

    public OutboxController(IOutboxAdministration administration)
    {
        _administration = administration;
    }

    [HttpGet]
    [ProducesResponseType(typeof(OutboxStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<OutboxStatus>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await _administration.GetStatusAsync(cancellationToken));
    }

    [HttpPost("{eventId:guid}/replay")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Replay(Guid eventId, CancellationToken cancellationToken)
    {
        return await _administration.ReplayAsync(eventId, cancellationToken)
            ? Accepted()
            : NotFound();
    }
}
