namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

public static class SaleListQueryParameters
{
    public const string Page = "_page";
    public const string PageSize = "_size";
    public const string Order = "_order";
    public const string SaleNumber = "saleNumber";
    public const string CustomerExternalId = "customerExternalId";
    public const string BranchExternalId = "branchExternalId";
    public const string IsCancelled = "isCancelled";
    public const string MinimumSaleDate = "_minSaleDate";
    public const string MaximumSaleDate = "_maxSaleDate";
    public const string MinimumTotalAmount = "_minTotalAmount";
    public const string MaximumTotalAmount = "_maxTotalAmount";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Page,
        PageSize,
        Order,
        SaleNumber,
        CustomerExternalId,
        BranchExternalId,
        IsCancelled,
        MinimumSaleDate,
        MaximumSaleDate,
        MinimumTotalAmount,
        MaximumTotalAmount
    };
}
