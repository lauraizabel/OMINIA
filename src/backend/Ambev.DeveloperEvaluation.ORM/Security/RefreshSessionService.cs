using System.Security.Cryptography;
using Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ambev.DeveloperEvaluation.ORM.Security;

public sealed class RefreshSessionService : IRefreshSessionService
{
    private readonly DefaultContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly RefreshSessionOptions _options;

    public RefreshSessionService(
        DefaultContext context,
        TimeProvider timeProvider,
        IOptions<RefreshSessionOptions> options)
    {
        _context = context;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<IssuedRefreshToken> IssueAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var absoluteExpiration = now.AddDays(_options.AbsoluteExpirationDays);
        var (rawToken, tokenHash) = CreateToken();
        var session = RefreshSession.Create(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            tokenHash,
            now,
            Earliest(now.AddDays(_options.IdleExpirationDays), absoluteExpiration),
            absoluteExpiration);

        _context.RefreshSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);
        return new IssuedRefreshToken(rawToken, session.ExpiresAt);
    }

    public async Task<RotatedRefreshToken> RotateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = Hash(token);
        var now = _timeProvider.GetUtcNow();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var current = await _context.RefreshSessions
            .FromSqlInterpolated($$"""
                SELECT * FROM "RefreshSessions"
                WHERE "TokenHash" = {{tokenHash}}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (current is null)
            throw InvalidSession();

        if (current.RevokedAt is not null)
        {
            if (current.ReplacedBySessionId is not null &&
                now - current.RevokedAt.Value > TimeSpan.FromSeconds(_options.ReuseGraceSeconds))
            {
                await RevokeFamilyAsync(current.FamilyId, now, "ReuseDetected", cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            throw InvalidSession();
        }

        if (current.ExpiresAt <= now || current.AbsoluteExpiresAt <= now)
        {
            current.Revoke(now, "Expired");
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidSession();
        }

        var user = await _context.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == current.UserId,
            cancellationToken);
        if (user?.Status != UserStatus.Active)
        {
            await RevokeFamilyAsync(current.FamilyId, now, "UserUnavailable", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidSession();
        }

        var (rawToken, replacementHash) = CreateToken();
        var replacementId = Guid.NewGuid();
        var replacement = RefreshSession.Create(
            replacementId,
            current.UserId,
            current.FamilyId,
            replacementHash,
            now,
            Earliest(now.AddDays(_options.IdleExpirationDays), current.AbsoluteExpiresAt),
            current.AbsoluteExpiresAt);

        current.Rotate(replacementId, now);
        _context.RefreshSessions.Add(replacement);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RotatedRefreshToken(user, rawToken, replacement.ExpiresAt);
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenHash = Hash(token);
        var now = _timeProvider.GetUtcNow();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var session = await _context.RefreshSessions
            .FromSqlInterpolated($$"""
                SELECT * FROM "RefreshSessions"
                WHERE "TokenHash" = {{tokenHash}}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await RevokeFamilyAsync(
            session.FamilyId,
            now,
            "Logout",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken) =>
        _context.RefreshSessions
            .Where(session => session.FamilyId == familyId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(session => session.RevokedAt, now)
                .SetProperty(session => session.RevocationReason, reason), cancellationToken);

    private (string RawToken, string TokenHash) CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(_options.TokenSizeBytes);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return (token, Hash(token));
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static DateTimeOffset Earliest(DateTimeOffset first, DateTimeOffset second) =>
        first <= second ? first : second;

    private static UnauthorizedAccessException InvalidSession() =>
        new("The refresh session is invalid or expired.");
}
