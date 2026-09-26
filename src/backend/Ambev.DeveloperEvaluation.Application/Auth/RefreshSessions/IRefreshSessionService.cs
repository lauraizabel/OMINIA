using Ambev.DeveloperEvaluation.Domain.Entities;

namespace Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;

public interface IRefreshSessionService
{
    Task<IssuedRefreshToken> IssueAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<RotatedRefreshToken> RotateAsync(string token, CancellationToken cancellationToken = default);
    Task RevokeAsync(string token, CancellationToken cancellationToken = default);
}

public sealed record IssuedRefreshToken(string Token, DateTimeOffset ExpiresAt);

public sealed record RotatedRefreshToken(User User, string Token, DateTimeOffset ExpiresAt);
