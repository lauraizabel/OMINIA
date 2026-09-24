using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Primitives;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

public static partial class SaleListRequestParser
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 10;
    private const int MaximumPageSize = 100;
    private const int MaximumRepeatedValues = 20;
    private const decimal MaximumMoney = 2_000_000_000m;

    private static readonly HashSet<string> AllowedParameters = new(StringComparer.Ordinal)
    {
        "_page",
        "_size",
        "_order",
        "saleNumber",
        "customerExternalId",
        "branchExternalId",
        "isCancelled",
        "_minSaleDate",
        "_maxSaleDate",
        "_minTotalAmount",
        "_maxTotalAmount"
    };

    public static ListSalesQuery Parse(IQueryCollection query)
    {
        ArgumentNullException.ThrowIfNull(query);
        RejectUnknownParameters(query);

        var page = ParsePositiveInteger(query, "_page", DefaultPage, SaleListErrorCodes.InvalidPage);
        var pageSize = ParsePositiveInteger(query, "_size", DefaultPageSize, SaleListErrorCodes.InvalidPageSize);
        if (pageSize > MaximumPageSize)
            Fail("_size", SaleListErrorCodes.InvalidPageSize, $"Page size cannot exceed {MaximumPageSize}.");

        if ((long)(page - 1) * pageSize > int.MaxValue)
            Fail("_page", SaleListErrorCodes.InvalidPage, "The requested page is too large.");

        var minimumSaleDate = ParseDate(query, "_minSaleDate");
        var maximumSaleDate = ParseDate(query, "_maxSaleDate");
        if (minimumSaleDate > maximumSaleDate)
            Fail("_minSaleDate", SaleListErrorCodes.InvalidRange, "The minimum sale date cannot exceed the maximum sale date.");

        var minimumTotal = ParseMoney(query, "_minTotalAmount");
        var maximumTotal = ParseMoney(query, "_maxTotalAmount");
        if (minimumTotal > maximumTotal)
            Fail("_minTotalAmount", SaleListErrorCodes.InvalidRange, "The minimum total cannot exceed the maximum total.");

        return new ListSalesQuery(new SaleListCriteria(
            page,
            pageSize,
            ParseOrder(query),
            ParseSaleNumber(query),
            ParseExternalIds(query, "customerExternalId"),
            ParseExternalIds(query, "branchExternalId"),
            ParseCancellationStates(query),
            minimumSaleDate,
            maximumSaleDate,
            minimumTotal,
            maximumTotal));
    }

    private static void RejectUnknownParameters(IQueryCollection query)
    {
        foreach (var parameter in query.Keys)
        {
            if (!AllowedParameters.Contains(parameter))
                Fail(parameter, SaleListErrorCodes.UnknownParameter, $"Query parameter '{parameter}' is not supported.");
        }
    }

    private static int ParsePositiveInteger(
        IQueryCollection query,
        string name,
        int defaultValue,
        string errorCode)
    {
        var raw = ReadSingle(query, name);
        if (raw is null)
            return defaultValue;

        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
            Fail(name, errorCode, $"{name} must be a positive integer.");

        return value;
    }

    private static IReadOnlyCollection<SaleOrder> ParseOrder(IQueryCollection query)
    {
        var raw = ReadSingle(query, "_order");
        if (raw is null)
        {
            return
            [
                new SaleOrder(SaleOrderField.SaleDate, SortDirection.Descending),
                new SaleOrder(SaleOrderField.Id, SortDirection.Ascending)
            ];
        }

        if (string.IsNullOrWhiteSpace(raw))
            Fail("_order", SaleListErrorCodes.InvalidOrder, "Ordering cannot be empty.");

        var terms = new List<SaleOrder>();
        var seen = new HashSet<SaleOrderField>();
        foreach (var segment in raw.Split(',', StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var field = default(SaleOrderField);
            if (parts.Length is < 1 or > 2 || !TryParseOrderField(parts[0], out field))
                Fail("_order", SaleListErrorCodes.InvalidOrder, $"Ordering term '{segment}' is invalid.");

            var direction = parts.Length == 1 ? SortDirection.Ascending : ParseDirection(parts[1]);
            if (!seen.Add(field))
                Fail("_order", SaleListErrorCodes.InvalidOrder, $"Ordering field '{parts[0]}' cannot be repeated.");

            if (seen.Contains(SaleOrderField.Id) && field != SaleOrderField.Id)
                Fail("_order", SaleListErrorCodes.InvalidOrder, "The id ordering field must be the final term.");

            terms.Add(new SaleOrder(field, direction));
        }

        if (terms.Count == 0)
            Fail("_order", SaleListErrorCodes.InvalidOrder, "Ordering cannot be empty.");

        if (!seen.Contains(SaleOrderField.Id))
            terms.Add(new SaleOrder(SaleOrderField.Id, SortDirection.Ascending));

        return terms;
    }

    private static bool TryParseOrderField(string value, out SaleOrderField field)
    {
        field = value switch
        {
            "saleDate" => SaleOrderField.SaleDate,
            "saleNumber" => SaleOrderField.SaleNumber,
            "totalAmount" => SaleOrderField.TotalAmount,
            "id" => SaleOrderField.Id,
            _ => default
        };
        return value is "saleDate" or "saleNumber" or "totalAmount" or "id";
    }

    private static SortDirection ParseDirection(string value)
    {
        if (value.Equals("asc", StringComparison.OrdinalIgnoreCase))
            return SortDirection.Ascending;
        if (value.Equals("desc", StringComparison.OrdinalIgnoreCase))
            return SortDirection.Descending;

        Fail("_order", SaleListErrorCodes.InvalidOrder, $"Ordering direction '{value}' is invalid.");
        return default;
    }

    private static SaleNumberFilter? ParseSaleNumber(IQueryCollection query)
    {
        var raw = ReadSingle(query, "saleNumber");
        if (raw is null)
            return null;

        var normalized = raw.Trim().ToUpperInvariant();
        if (normalized.Length is 0 or > 52)
            Fail("saleNumber", SaleListErrorCodes.InvalidFilter, "Sale number must contain between 1 and 52 characters including wildcards.");

        var matchStart = normalized.StartsWith('*');
        var matchEnd = normalized.EndsWith('*');
        var valueStart = matchStart ? 1 : 0;
        var valueEnd = normalized.Length - (matchEnd ? 1 : 0);
        if (valueEnd <= valueStart)
            Fail("saleNumber", SaleListErrorCodes.InvalidFilter, "Sale number must contain a value in addition to wildcards.");

        var value = normalized[valueStart..valueEnd];
        if (value.Length is 0 or > 50 || value.Contains('*') || value.Any(char.IsControl))
            Fail("saleNumber", SaleListErrorCodes.InvalidFilter, "Sale number permits one wildcard only at either edge.");

        return new SaleNumberFilter(value, matchStart, matchEnd);
    }

    private static IReadOnlyCollection<string> ParseExternalIds(IQueryCollection query, string name)
    {
        if (!query.TryGetValue(name, out var values))
            return [];

        EnsureValueCount(name, values);
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in values)
        {
            var value = raw?.Trim() ?? string.Empty;
            if (value.Length is 0 || value.Length > ExternalIdentity.ExternalIdMaximumLength || value.Any(char.IsControl))
                Fail(name, SaleListErrorCodes.InvalidFilter, $"{name} contains an invalid external ID.");
            normalized.Add(value);
        }

        return normalized.ToArray();
    }

    private static IReadOnlyCollection<bool> ParseCancellationStates(IQueryCollection query)
    {
        if (!query.TryGetValue("isCancelled", out var values))
            return [];

        EnsureValueCount("isCancelled", values);
        var states = new HashSet<bool>();
        foreach (var raw in values)
        {
            if (!bool.TryParse(raw, out var value))
                Fail("isCancelled", SaleListErrorCodes.InvalidFilter, "isCancelled values must be true or false.");
            states.Add(value);
        }

        return states.ToArray();
    }

    private static DateTimeOffset? ParseDate(IQueryCollection query, string name)
    {
        var raw = ReadSingle(query, name);
        if (raw is null)
            return null;

        var candidate = raw.Trim();
        var value = default(DateTimeOffset);
        if (!OffsetSuffix().IsMatch(candidate) ||
            !DateTimeOffset.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            Fail(name, SaleListErrorCodes.InvalidDate, $"{name} must be an ISO 8601 date-time with an explicit offset.");
        }

        return value.ToUniversalTime();
    }

    private static decimal? ParseMoney(IQueryCollection query, string name)
    {
        var raw = ReadSingle(query, name);
        if (raw is null)
            return null;

        const NumberStyles style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (!decimal.TryParse(raw, style, CultureInfo.InvariantCulture, out var value) ||
            value < 0 || value > MaximumMoney || decimal.Round(value, 2) != value)
        {
            Fail(name, SaleListErrorCodes.InvalidMoney, $"{name} must be between 0 and {MaximumMoney:F2} with at most two decimal places.");
        }

        return value;
    }

    private static string? ReadSingle(IQueryCollection query, string name)
    {
        if (!query.TryGetValue(name, out var values))
            return null;
        if (values.Count != 1)
            Fail(name, SaleListErrorCodes.DuplicateParameter, $"Query parameter '{name}' cannot be repeated.");
        return values[0];
    }

    private static void EnsureValueCount(string name, StringValues values)
    {
        if (values.Count is 0 or > MaximumRepeatedValues)
            Fail(name, SaleListErrorCodes.TooManyValues, $"Query parameter '{name}' accepts at most {MaximumRepeatedValues} values.");
    }

    [DoesNotReturn]
    private static void Fail(string field, string code, string message) =>
        throw new ValidationException([new ValidationFailure(field, message) { ErrorCode = code }]);

    [GeneratedRegex(@"(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex OffsetSuffix();
}
