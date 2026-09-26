using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;

public sealed record RefreshUserSessionCommand(string RefreshToken) : IRequest<RefreshUserSessionResult>;

public sealed record RefreshUserSessionResult(
    string Token,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    string Email,
    string Name,
    string Role);
