namespace Ambev.DeveloperEvaluation.WebApi.Security;

public sealed class LoginRateLimitOptions
{
    public const string SectionName = "Security:LoginRateLimit";

    public int PermitLimit { get; init; } = 5;
    public int WindowSeconds { get; init; } = 60;
}
