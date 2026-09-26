using Ambev.DeveloperEvaluation.Domain.Entities;

namespace Ambev.DeveloperEvaluation.ORM.Security;

public sealed class RefreshSession
{
    private RefreshSession()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset AbsoluteExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }
    public Guid? ReplacedBySessionId { get; private set; }
    public User User { get; private set; } = null!;

    public static RefreshSession Create(
        Guid id,
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset absoluteExpiresAt) => new()
        {
            Id = id,
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            AbsoluteExpiresAt = absoluteExpiresAt
        };

    public void Rotate(Guid replacementId, DateTimeOffset now)
    {
        RevokedAt = now;
        RevocationReason = "Rotated";
        ReplacedBySessionId = replacementId;
    }

    public void Revoke(DateTimeOffset now, string reason)
    {
        RevokedAt ??= now;
        RevocationReason ??= reason;
    }
}
