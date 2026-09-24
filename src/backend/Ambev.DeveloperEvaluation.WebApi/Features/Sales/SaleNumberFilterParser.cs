using Ambev.DeveloperEvaluation.Application.Sales.ListSales;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

internal static class SaleNumberFilterParser
{
    public static SaleNumberFilter? Parse(string? raw)
    {
        if (raw is null)
            return null;

        var normalized = raw.Trim().ToUpperInvariant();
        if (normalized.Length is 0 or > 52)
            Fail("Sale number must contain between 1 and 52 characters including wildcards.");

        var matchStart = normalized.StartsWith('*');
        var matchEnd = normalized.EndsWith('*');
        var valueStart = matchStart ? 1 : 0;
        var valueEnd = normalized.Length - (matchEnd ? 1 : 0);
        if (valueEnd <= valueStart)
            Fail("Sale number must contain a value in addition to wildcards.");

        var value = normalized[valueStart..valueEnd];
        if (value.Length is 0 or > 50 || value.Contains('*') || value.Any(char.IsControl))
            Fail("Sale number permits one wildcard only at either edge.");

        return new SaleNumberFilter(value, matchStart, matchEnd);
    }

    private static void Fail(string message) => SaleListQueryValidation.Fail(
        SaleListQueryParameters.SaleNumber,
        SaleListErrorCodes.InvalidFilter,
        message);
}
