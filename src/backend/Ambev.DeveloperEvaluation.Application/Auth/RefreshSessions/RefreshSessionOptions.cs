namespace Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;

public sealed class RefreshSessionOptions
{
    public const string SectionName = "RefreshSession";

    public int IdleExpirationDays { get; init; } = 7;
    public int AbsoluteExpirationDays { get; init; } = 30;
    public int TokenSizeBytes { get; init; } = 64;
    public int ReuseGraceSeconds { get; init; } = 10;
    public string CookieName { get; init; } = "__Host-developerstore-refresh";
    public bool SecureCookie { get; init; } = true;
}
