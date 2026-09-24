using System.Globalization;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

public static class SaleEtag
{
    private const string Prefix = "\"sale-";

    public static string Create(long version) => $"{Prefix}{version.ToString(CultureInfo.InvariantCulture)}\"";

    public static long ParseRequired(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw HttpPreconditionException.Required();

        var candidate = value.Trim();
        if (!candidate.StartsWith(Prefix, StringComparison.Ordinal) ||
            !candidate.EndsWith('"') ||
            candidate.Contains(',') ||
            !long.TryParse(
                candidate.AsSpan(Prefix.Length, candidate.Length - Prefix.Length - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var version) ||
            version <= 0)
        {
            throw HttpPreconditionException.Malformed();
        }

        return version;
    }
}
