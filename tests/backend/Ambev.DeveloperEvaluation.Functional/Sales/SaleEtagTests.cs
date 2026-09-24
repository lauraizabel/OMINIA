using Ambev.DeveloperEvaluation.WebApi.Features.Sales;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Sales;

public sealed class SaleEtagTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(long.MaxValue)]
    public void Created_etag_round_trips(long version)
    {
        var etag = SaleEtag.Create(version);

        Assert.Equal(version, SaleEtag.ParseRequired(etag));
        Assert.StartsWith("\"sale-", etag, StringComparison.Ordinal);
        Assert.EndsWith("\"", etag, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("W/\"sale-1\"")]
    [InlineData("\"sale-1\", \"sale-2\"")]
    [InlineData("\"sale-zero\"")]
    [InlineData("\"sale-0\"")]
    public void Malformed_or_unsupported_if_match_is_rejected(string value)
    {
        var exception = Assert.Throws<HttpPreconditionException>(() => SaleEtag.ParseRequired(value));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_if_match_requires_precondition(string? value)
    {
        var exception = Assert.Throws<HttpPreconditionException>(() => SaleEtag.ParseRequired(value));

        Assert.Equal(StatusCodes.Status428PreconditionRequired, exception.StatusCode);
    }
}
