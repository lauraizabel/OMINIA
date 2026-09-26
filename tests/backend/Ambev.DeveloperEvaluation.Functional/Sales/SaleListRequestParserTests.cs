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
    [InlineData("?_order=%20%20", SaleListErrorCodes.InvalidOrder)]
    [InlineData("?_order=saleDate%20sideways", SaleListErrorCodes.InvalidOrder)]
    [InlineData("?_order=saleDate,saleDate", SaleListErrorCodes.InvalidOrder)]
    [InlineData("?_order=saleDate%20asc%20extra", SaleListErrorCodes.InvalidOrder)]
    [InlineData("?saleNumber=SALE*INNER", SaleListErrorCodes.InvalidFilter)]
    [InlineData("?saleNumber=%20", SaleListErrorCodes.InvalidFilter)]
    [InlineData("?saleNumber=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", SaleListErrorCodes.InvalidFilter)]
    [InlineData("?customerExternalId=%20", SaleListErrorCodes.InvalidFilter)]
    [InlineData("?isCancelled=maybe", SaleListErrorCodes.InvalidFilter)]
    [InlineData("?_minTotalAmount=-1", SaleListErrorCodes.InvalidMoney)]
    [InlineData("?_maxTotalAmount=2000000001", SaleListErrorCodes.InvalidMoney)]
    [InlineData("?_maxTotalAmount=not-money", SaleListErrorCodes.InvalidMoney)]
    [InlineData("?_minSaleDate=2026-99-99T10%3A00%3A00Z", SaleListErrorCodes.InvalidDate)]
    public void Invalid_query_returns_centralized_validation_code(string query, string expectedCode)
    {
        var exception = Assert.Throws<ValidationException>(() => Parse(query));

        Assert.Equal(expectedCode, Assert.Single(exception.Errors).ErrorCode);
    }

    [Theory]
    [InlineData("?saleNumber=SALE-1", "SALE-1", false, false)]
    [InlineData("?saleNumber=*SALE-1", "SALE-1", true, false)]
    [InlineData("?saleNumber=SALE-1*", "SALE-1", false, true)]
    public void Sale_number_wildcards_are_normalized(
        string query,
        string value,
        bool matchStart,
        bool matchEnd)
    {
        var filter = Parse(query).Criteria.SaleNumber;
        Assert.Equal(new SaleNumberFilter(value, matchStart, matchEnd), filter);
    }

    [Theory]
    [InlineData("saleDate asc", SaleOrderField.SaleDate, SortDirection.Ascending)]
    [InlineData("saleNumber DESC", SaleOrderField.SaleNumber, SortDirection.Descending)]
    [InlineData("totalAmount", SaleOrderField.TotalAmount, SortDirection.Ascending)]
    [InlineData("id desc", SaleOrderField.Id, SortDirection.Descending)]
    public void Supported_order_terms_accept_explicit_and_implicit_directions(
        string raw,
        SaleOrderField field,
        SortDirection direction)
    {
        var order = Parse("?_order=" + Uri.EscapeDataString(raw)).Criteria.Order.First();
        Assert.Equal(new SaleOrder(field, direction), order);
    }

    [Fact]
    public void Null_query_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => SaleListRequestParser.Parse(null!));
    }

    [Fact]
    public void Repeated_filters_enforce_the_twenty_value_limit()
    {
        var repeated = string.Join('&', Enumerable.Range(1, 21).Select(i => $"customerExternalId=C-{i}"));
        var exception = Assert.Throws<ValidationException>(() => Parse("?" + repeated));
        Assert.Equal(SaleListErrorCodes.TooManyValues, Assert.Single(exception.Errors).ErrorCode);
    }

    [Fact]
    public void Filter_values_enforce_domain_length_limits_after_wildcard_parsing()
    {
        var saleNumber = Assert.Throws<ValidationException>(() =>
            Parse("?saleNumber=" + new string('A', 51)));
        Assert.Equal(SaleListErrorCodes.InvalidFilter, Assert.Single(saleNumber.Errors).ErrorCode);

        var externalId = Assert.Throws<ValidationException>(() =>
            Parse("?customerExternalId=" + new string('A', 101)));
        Assert.Equal(SaleListErrorCodes.InvalidFilter, Assert.Single(externalId.Errors).ErrorCode);
    }

    private static ListSalesQuery Parse(string queryString)
    {
        var values = QueryHelpers.ParseQuery(queryString);
        return SaleListRequestParser.Parse(new QueryCollection(values));
    }
}
