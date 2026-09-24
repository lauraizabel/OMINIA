using System.Globalization;
using System.Text.RegularExpressions;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using Microsoft.Extensions.Primitives;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

public static partial class SaleListRequestParser
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 10;
    private const int MaximumPageSize = 100;
    private const int MaximumRepeatedValues = 20;
    private const decimal MaximumMoney = 2_000_000_000m;

    public static ListSalesQuery Parse(IQueryCollection query)
    {
        ArgumentNullException.ThrowIfNull(query);
        RejectUnknownParameters(query);

        var page = ParsePositiveInteger(query, SaleListQueryParameters.Page, DefaultPage, SaleListErrorCodes.InvalidPage);
        var pageSize = ParsePositiveInteger(query, SaleListQueryParameters.PageSize, DefaultPageSize, SaleListErrorCodes.InvalidPageSize);
        if (pageSize > MaximumPageSize)
            Fail(SaleListQueryParameters.PageSize, SaleListErrorCodes.InvalidPageSize, $"Page size cannot exceed {MaximumPageSize}.");

        if ((long)(page - 1) * pageSize > int.MaxValue)
            Fail(SaleListQueryParameters.Page, SaleListErrorCodes.InvalidPage, "The requested page is too large.");

        var minimumSaleDate = ParseDate(query, SaleListQueryParameters.MinimumSaleDate);
        var maximumSaleDate = ParseDate(query, SaleListQueryParameters.MaximumSaleDate);
        if (minimumSaleDate > maximumSaleDate)
            Fail(SaleListQueryParameters.MinimumSaleDate, SaleListErrorCodes.InvalidRange, "The minimum sale date cannot exceed the maximum sale date.");

        var minimumTotal = ParseMoney(query, SaleListQueryParameters.MinimumTotalAmount);
        var maximumTotal = ParseMoney(query, SaleListQueryParameters.MaximumTotalAmount);
        if (minimumTotal > maximumTotal)
            Fail(SaleListQueryParameters.MinimumTotalAmount, SaleListErrorCodes.InvalidRange, "The minimum total cannot exceed the maximum total.");

        return new ListSalesQuery(new SaleListCriteria(
            page,
            pageSize,
            SaleOrderParser.Parse(ReadSingle(query, SaleListQueryParameters.Order)),
            SaleNumberFilterParser.Parse(ReadSingle(query, SaleListQueryParameters.SaleNumber)),
            ParseExternalIds(query, SaleListQueryParameters.CustomerExternalId),
            ParseExternalIds(query, SaleListQueryParameters.BranchExternalId),
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
            if (!SaleListQueryParameters.All.Contains(parameter))
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
        if (!query.TryGetValue(SaleListQueryParameters.IsCancelled, out var values))
            return [];

        EnsureValueCount(SaleListQueryParameters.IsCancelled, values);
        var states = new HashSet<bool>();
        foreach (var raw in values)
        {
            if (!bool.TryParse(raw, out var value))
                Fail(SaleListQueryParameters.IsCancelled, SaleListErrorCodes.InvalidFilter, "isCancelled values must be true or false.");
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

    private static void Fail(string field, string code, string message) =>
        SaleListQueryValidation.Fail(field, code, message);

    [GeneratedRegex(@"(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex OffsetSuffix();
}
