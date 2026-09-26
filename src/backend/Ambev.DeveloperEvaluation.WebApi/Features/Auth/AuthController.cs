using MediatR;
using Microsoft.AspNetCore.Mvc;
using Ambev.DeveloperEvaluation.WebApi.Common;
using Ambev.DeveloperEvaluation.WebApi.Features.Auth.AuthenticateUserFeature;
using Ambev.DeveloperEvaluation.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Auth;

/// <summary>
/// Controller for authentication operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController : BaseController
{
    private readonly IMediator _mediator;
    private readonly IRefreshSessionService _refreshSessions;
    private readonly RefreshTokenCookieManager _refreshCookie;

    /// <summary>
    /// Initializes a new instance of AuthController
    /// </summary>
    /// <param name="mediator">The mediator instance</param>
    public AuthController(
        IMediator mediator,
        IRefreshSessionService refreshSessions,
        RefreshTokenCookieManager refreshCookie)
    {
        _mediator = mediator;
        _refreshSessions = refreshSessions;
        _refreshCookie = refreshCookie;
    }

    /// <summary>
    /// Authenticates a user with their credentials
    /// </summary>
    /// <param name="request">The authentication request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication token if successful</returns>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType(typeof(ApiResponseWithData<AuthenticateUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> AuthenticateUser([FromBody] AuthenticateUserRequest request, CancellationToken cancellationToken)
    {
        if (!_refreshCookie.IsTrustedOrigin(Request))
            return StatusCode(StatusCodes.Status403Forbidden);

        var command = request.ToCommand();
        var response = await _mediator.Send(command, cancellationToken);
        _refreshCookie.Write(Response, response.RefreshToken, response.RefreshTokenExpiresAt);

        return base.Ok(new ApiResponseWithData<AuthenticateUserResponse>
        {
            Success = true,
            Message = "User authenticated successfully",
            Data = response.ToResponse()
        });
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Session)]
    [ProducesResponseType(typeof(ApiResponseWithData<AuthenticateUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!_refreshCookie.IsTrustedOrigin(Request))
            return StatusCode(StatusCodes.Status403Forbidden);

        var currentToken = _refreshCookie.Read(Request);
        if (string.IsNullOrWhiteSpace(currentToken))
            return Unauthorized();

        var result = await _mediator.Send(
            new RefreshUserSessionCommand(currentToken),
            cancellationToken);
        _refreshCookie.Write(Response, result.RefreshToken, result.RefreshTokenExpiresAt);

        return base.Ok(new ApiResponseWithData<AuthenticateUserResponse>
        {
            Success = true,
            Message = "Session refreshed successfully",
            Data = new AuthenticateUserResponse
            {
                Token = result.Token,
                Email = result.Email,
                Name = result.Name,
                Role = result.Role
            }
        });
    }

    [HttpPost("logout")]
    [EnableRateLimiting(RateLimitPolicies.Session)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!_refreshCookie.IsTrustedOrigin(Request))
            return StatusCode(StatusCodes.Status403Forbidden);

        var currentToken = _refreshCookie.Read(Request);
        if (!string.IsNullOrWhiteSpace(currentToken))
            await _refreshSessions.RevokeAsync(currentToken, cancellationToken);

        _refreshCookie.Delete(Response);
        return NoContent();
    }
}
