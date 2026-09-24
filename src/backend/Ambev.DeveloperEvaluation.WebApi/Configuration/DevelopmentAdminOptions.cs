namespace Ambev.DeveloperEvaluation.WebApi.Configuration;

public sealed class DevelopmentAdminOptions
{
    public const string SectionName = "DevelopmentAdmin";

    public bool Enabled { get; init; }
    public string Username { get; init; } = "Development Admin";
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = "+5500000000000";
    public string Password { get; init; } = string.Empty;
}
