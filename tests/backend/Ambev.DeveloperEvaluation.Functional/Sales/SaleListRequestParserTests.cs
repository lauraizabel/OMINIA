using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.WebApi.Features.Sales;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Sales;

public sealed class SaleListRequestParserTests
{
    [Fact]
    public void Empty_query_uses_stable_defaults()
    {
        var result = Parse(string.Empty).Criteria;

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Collection(
            result.Order,
            order => Assert.Equal(new SaleOrder(SaleOrderField.SaleDate, SortDirection.Descending), order),
            order => Assert.Equal(new SaleOrder(SaleOrderField.Id, SortDirection.Ascending), order));
    }

    [Fact]
    public void Valid_query_is_normalized_into_typed_criteria()
    {
        var result = Parse(
            "?_page=2&_size=25&_order=totalAmount%20desc,saleNumber" +
            "&saleNumber=*promo_%25*" +
            "&customerExternalId=%20CUSTOMER-2%20&customerExternalId=CUSTOMER-1" +
            "&branchExternalId=BRANCH-1&isCancelled=true&isCancelled=false" +
            "&_minSaleDate=2026-09-20T10%3A00%3A00-03%3A00" +
            "&_maxTotalAmount=100.50").Criteria;

        Assert.Equal(2, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(new SaleNumberFilter("PROMO_%", true, true), result.SaleNumber);
        Assert.Equal(["CUSTOMER-2", "CUSTOMER-1"], result.CustomerExternalIds);
        Assert.Equal([true, false], result.CancellationStates);
        Assert.Equal(100.50m, result.MaximumTotalAmount);
        Assert.Equal(SaleOrderField.Id, result.Order.Last().Field);
    }

    [Theory]
    [InlineData("?unknown=value", SaleListErrorCodes.UnknownParameter)]
    [InlineData("?_page=0", SaleListErrorCodes.InvalidPage)]
    [InlineData("?_page=2147483647&_size=100", SaleListErrorCodes.InvalidPage)]
    [InlineData("?_size=101", SaleListErrorCodes.InvalidPageSize)]
    [InlineData("?_order=updatedAt%20desc", SaleListErrorCodes.InvalidOrder)]
    [InlineData("?_order=id,saleDate", SaleListErrorCodes.InvalidOrder)]
    [InlineData("?saleNumber=SA*LE", SaleListErrorCodes.InvalidFilter)]
    [InlineData("?saleNumber=*", SaleListErrorCodes.InvalidFilter)]
    [InlineData("?_minSaleDate=2026-09-20T10%3A00%3A00", SaleListErrorCodes.InvalidDate)]
    [InlineData("?_minTotalAmount=10.001", SaleListErrorCodes.InvalidMoney)]
    [InlineData("?_minTotalAmount=20&_maxTotalAmount=10", SaleListErrorCodes.InvalidRange)]
    [InlineData("?_minSaleDate=2026-09-21T10%3A00%3A00Z&_maxSaleDate=2026-09-20T10%3A00%3A00Z", SaleListErrorCodes.InvalidRange)]
    [InlineData("?_minTotalAmount=1&_minTotalAmount=2", SaleListErrorCodes.DuplicateParameter)]
    public void Invalid_query_returns_centralized_validation_code(string query, string expectedCode)
    {
        var exception = Assert.Throws<ValidationException>(() => Parse(query));

        Assert.Equal(expectedCode, Assert.Single(exception.Errors).ErrorCode);
    }

    private static ListSalesQuery Parse(string queryString)
    {
        var values = QueryHelpers.ParseQuery(queryString);
        return SaleListRequestParser.Parse(new QueryCollection(values));
    }
}
