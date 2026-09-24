using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Microsoft.AspNetCore.Mvc;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

public sealed class ListSalesRequest
{
    [FromQuery(Name = "_page")]
    public int? Page { get; init; }

    [FromQuery(Name = "_size")]
    public int? PageSize { get; init; }

    [FromQuery(Name = "_order")]
    public string? Order { get; init; }

    [FromQuery(Name = "saleNumber")]
    public string? SaleNumber { get; init; }

    [FromQuery(Name = "customerExternalId")]
    public string[] CustomerExternalIds { get; init; } = [];

    [FromQuery(Name = "branchExternalId")]
    public string[] BranchExternalIds { get; init; } = [];

    [FromQuery(Name = "isCancelled")]
    public bool[] CancellationStates { get; init; } = [];

    [FromQuery(Name = "_minSaleDate")]
    public DateTimeOffset? MinimumSaleDate { get; init; }

    [FromQuery(Name = "_maxSaleDate")]
    public DateTimeOffset? MaximumSaleDate { get; init; }

    [FromQuery(Name = "_minTotalAmount")]
    public decimal? MinimumTotalAmount { get; init; }

    [FromQuery(Name = "_maxTotalAmount")]
    public decimal? MaximumTotalAmount { get; init; }
}

public sealed record ExternalIdentityRequest(string ExternalId, string Name)
{
    public ExternalIdentityInput ToApplication() => new(ExternalId, Name);
}

public sealed record CreateSaleItemRequest(
    ExternalIdentityRequest Product,
    int Quantity,
    decimal UnitPrice)
{
    public SaleItemInput ToApplication() => new(null, Product.ToApplication(), Quantity, UnitPrice);
}

public sealed record UpdateSaleItemRequest(
    Guid? Id,
    ExternalIdentityRequest Product,
    int Quantity,
    decimal UnitPrice)
{
    public SaleItemInput ToApplication() => new(Id, Product.ToApplication(), Quantity, UnitPrice);
}

public sealed record CreateSaleRequest(
    string SaleNumber,
    DateTimeOffset SaleDate,
    ExternalIdentityRequest Customer,
    ExternalIdentityRequest Branch,
    IReadOnlyCollection<CreateSaleItemRequest> Items);

public sealed record UpdateSaleRequest(
    DateTimeOffset SaleDate,
    ExternalIdentityRequest Customer,
    ExternalIdentityRequest Branch,
    IReadOnlyCollection<UpdateSaleItemRequest> Items);

public sealed record ExternalIdentityResponse(string ExternalId, string Name);

public sealed record SaleSummaryResponse(
    Guid Id,
    string SaleNumber,
    DateTimeOffset SaleDate,
    ExternalIdentityResponse Customer,
    ExternalIdentityResponse Branch,
    decimal TotalAmount,
    bool IsCancelled,
    DateTimeOffset UpdatedAt,
    long Version)
{
    public static SaleSummaryResponse From(SaleSummaryResult result) => new(
        result.Id,
        result.SaleNumber,
        result.SaleDate,
        new ExternalIdentityResponse(result.Customer.ExternalId, result.Customer.Name),
        new ExternalIdentityResponse(result.Branch.ExternalId, result.Branch.Name),
        result.TotalAmount,
        result.IsCancelled,
        result.UpdatedAt,
        result.Version);
}

public sealed record PagedSalesResponse(
    IReadOnlyCollection<SaleSummaryResponse> Data,
    long TotalItems,
    int CurrentPage,
    long TotalPages)
{
    public static PagedSalesResponse From(PagedSalesResult result) => new(
        result.Data.Select(SaleSummaryResponse.From).ToArray(),
        result.TotalItems,
        result.CurrentPage,
        result.TotalPages);
}

public sealed record SaleItemResponse(
    Guid Id,
    ExternalIdentityResponse Product,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountRate,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal EffectiveAmount,
    bool IsCancelled,
    DateTimeOffset? CancelledAt);

public sealed record SaleResponse(
    Guid Id,
    string SaleNumber,
    DateTimeOffset SaleDate,
    ExternalIdentityResponse Customer,
    ExternalIdentityResponse Branch,
    IReadOnlyCollection<SaleItemResponse> Items,
    decimal TotalAmount,
    bool IsCancelled,
    DateTimeOffset? CancelledAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Version)
{
    public static SaleResponse From(SaleResult result) => new(
        result.Id,
        result.SaleNumber,
        result.SaleDate,
        new ExternalIdentityResponse(result.Customer.ExternalId, result.Customer.Name),
        new ExternalIdentityResponse(result.Branch.ExternalId, result.Branch.Name),
        result.Items.Select(item => new SaleItemResponse(
            item.Id,
            new ExternalIdentityResponse(item.Product.ExternalId, item.Product.Name),
            item.Quantity,
            item.UnitPrice,
            item.DiscountRate,
            item.GrossAmount,
            item.DiscountAmount,
            item.TotalAmount,
            item.EffectiveAmount,
            item.IsCancelled,
            item.CancelledAt)).ToArray(),
        result.TotalAmount,
        result.IsCancelled,
        result.CancelledAt,
        result.CreatedAt,
        result.UpdatedAt,
        result.Version);
}
