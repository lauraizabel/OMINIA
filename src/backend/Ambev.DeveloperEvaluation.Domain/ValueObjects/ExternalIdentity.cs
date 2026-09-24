using Ambev.DeveloperEvaluation.Domain.Exceptions;

namespace Ambev.DeveloperEvaluation.Domain.ValueObjects;

public sealed record ExternalIdentity
{
    public const int ExternalIdMaximumLength = 100;
    public const int NameMaximumLength = 200;

    public string ExternalId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    private ExternalIdentity()
    {
    }

    private ExternalIdentity(string externalId, string name)
    {
        ExternalId = externalId;
        Name = name;
    }

    public static ExternalIdentity Create(string? externalId, string? name)
    {
        var normalizedId = Normalize(
            externalId,
            ExternalIdMaximumLength,
            "ExternalIdentity.InvalidId",
            "External ID");

        var normalizedName = Normalize(
            name,
            NameMaximumLength,
            "ExternalIdentity.InvalidName",
            "External identity name");

        return new ExternalIdentity(normalizedId, normalizedName);
    }

    private static string Normalize(string? value, int maximumLength, string code, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainValidationException(code, $"{fieldName} is required.");

        var normalized = value.Trim();

        if (normalized.Length > maximumLength)
            throw new DomainValidationException(
                code,
                $"{fieldName} must contain at most {maximumLength} characters.");

        if (normalized.Any(char.IsControl))
            throw new DomainValidationException(code, $"{fieldName} cannot contain control characters.");

        return normalized;
    }
}
