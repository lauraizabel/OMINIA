using Ambev.DeveloperEvaluation.Common.Security;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;

public sealed class RefreshUserSessionHandler
    : IRequestHandler<RefreshUserSessionCommand, RefreshUserSessionResult>
{
    private readonly IRefreshSessionService _sessions;
    private readonly IJwtTokenGenerator _tokens;

    public RefreshUserSessionHandler(IRefreshSessionService sessions, IJwtTokenGenerator tokens)
    {
        _sessions = sessions;
        _tokens = tokens;
    }

    public async Task<RefreshUserSessionResult> Handle(
        RefreshUserSessionCommand request,
        CancellationToken cancellationToken)
    {
        var rotated = await _sessions.RotateAsync(request.RefreshToken, cancellationToken);
        var user = rotated.User;

        return new RefreshUserSessionResult(
            _tokens.GenerateToken(user),
            rotated.Token,
            rotated.ExpiresAt,
            user.Email,
            user.Username,
            user.Role.ToString());
    }
}
