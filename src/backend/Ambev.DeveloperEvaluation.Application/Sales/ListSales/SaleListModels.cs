namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public static class SaleListErrorCodes
{
    public const string DuplicateParameter = "SalesQuery.DuplicateParameter";
    public const string InvalidDate = "SalesQuery.InvalidDate";
    public const string InvalidFilter = "SalesQuery.InvalidFilter";
    public const string InvalidMoney = "SalesQuery.InvalidMoney";
    public const string InvalidOrder = "SalesQuery.InvalidOrder";
    public const string InvalidPage = "SalesQuery.InvalidPage";
    public const string InvalidPageSize = "SalesQuery.InvalidPageSize";
    public const string InvalidRange = "SalesQuery.InvalidRange";
    public const string TooManyValues = "SalesQuery.TooManyValues";
    public const string UnknownParameter = "SalesQuery.UnknownParameter";
}

public enum SaleOrderField
{
    SaleDate,
    SaleNumber,
    TotalAmount,
    Id
}

public enum SortDirection
{
    Ascending,
    Descending
}

public sealed record SaleOrder(SaleOrderField Field, SortDirection Direction);

public sealed record SaleNumberFilter(string Value, bool MatchStart, bool MatchEnd);

public sealed record SaleListCriteria(
    int Page,
    int PageSize,
    IReadOnlyCollection<SaleOrder> Order,
    SaleNumberFilter? SaleNumber,
    IReadOnlyCollection<string> CustomerExternalIds,
    IReadOnlyCollection<string> BranchExternalIds,
    IReadOnlyCollection<bool> CancellationStates,
    DateTimeOffset? MinimumSaleDate,
    DateTimeOffset? MaximumSaleDate,
    decimal? MinimumTotalAmount,
    decimal? MaximumTotalAmount);

public sealed record SaleSummaryResult(
    Guid Id,
    string SaleNumber,
    DateTimeOffset SaleDate,
    Common.ExternalIdentityResult Customer,
    Common.ExternalIdentityResult Branch,
    decimal TotalAmount,
    bool IsCancelled,
    DateTimeOffset UpdatedAt,
    long Version);

public sealed record PagedSalesResult(
    IReadOnlyCollection<SaleSummaryResult> Data,
    long TotalItems,
    int CurrentPage,
    long TotalPages);
