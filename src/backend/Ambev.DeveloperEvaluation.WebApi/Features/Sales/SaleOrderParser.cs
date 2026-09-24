using Ambev.DeveloperEvaluation.Application.Sales.ListSales;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

internal static class SaleOrderParser
{
    private static readonly IReadOnlyCollection<SaleOrder> DefaultOrder =
    [
        new SaleOrder(SaleOrderField.SaleDate, SortDirection.Descending),
        new SaleOrder(SaleOrderField.Id, SortDirection.Ascending)
    ];

    public static IReadOnlyCollection<SaleOrder> Parse(string? raw)
    {
        if (raw is null)
            return DefaultOrder;

        if (string.IsNullOrWhiteSpace(raw))
            Fail("Ordering cannot be empty.");

        var terms = new List<SaleOrder>();
        var seen = new HashSet<SaleOrderField>();
        foreach (var segment in raw.Split(',', StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var field = default(SaleOrderField);
            if (parts.Length is < 1 or > 2 || !TryParseField(parts[0], out field))
                Fail($"Ordering term '{segment}' is invalid.");

            if (!seen.Add(field))
                Fail($"Ordering field '{parts[0]}' cannot be repeated.");

            if (seen.Contains(SaleOrderField.Id) && field != SaleOrderField.Id)
                Fail("The id ordering field must be the final term.");

            terms.Add(new SaleOrder(
                field,
                parts.Length == 1 ? SortDirection.Ascending : ParseDirection(parts[1])));
        }

        if (terms.Count == 0)
            Fail("Ordering cannot be empty.");

        if (!seen.Contains(SaleOrderField.Id))
            terms.Add(new SaleOrder(SaleOrderField.Id, SortDirection.Ascending));

        return terms;
    }

    private static bool TryParseField(string value, out SaleOrderField field)
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

        Fail($"Ordering direction '{value}' is invalid.");
        return default;
    }

    private static void Fail(string message) => SaleListQueryValidation.Fail(
        SaleListQueryParameters.Order,
        SaleListErrorCodes.InvalidOrder,
        message);
}
