using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public sealed class ListSalesQueryValidatorTests
{
    [Fact]
    public async Task Valid_typed_criteria_passes_application_validation()
    {
        var result = await new ListSalesQueryValidator().ValidateAsync(new ListSalesQuery(ValidCriteria()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Invalid_typed_criteria_is_rejected_outside_the_http_parser()
    {
        var invalid = ValidCriteria() with
        {
            Page = int.MaxValue,
            PageSize = 100,
            Order = [new SaleOrder(SaleOrderField.SaleDate, SortDirection.Ascending)],
            MinimumTotalAmount = 20m,
            MaximumTotalAmount = 10m
        };

        var result = await new ListSalesQueryValidator().ValidateAsync(new ListSalesQuery(invalid));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorCode == SaleListErrorCodes.InvalidPage);
        Assert.Contains(result.Errors, error => error.ErrorCode == SaleListErrorCodes.InvalidOrder);
        Assert.Contains(result.Errors, error => error.ErrorCode == SaleListErrorCodes.InvalidRange);
    }

    private static SaleListCriteria ValidCriteria() => new(
        1,
        10,
        [
            new SaleOrder(SaleOrderField.SaleDate, SortDirection.Descending),
            new SaleOrder(SaleOrderField.Id, SortDirection.Ascending)
        ],
        null,
        [],
        [],
        [],
        null,
        null,
        null,
        null);
}
