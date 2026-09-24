using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.WebApi;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Sales;

public sealed class SalesEndpointTests
{
    private static readonly Guid SaleId = Guid.Parse("99cc2492-c255-44c8-8eb8-87709bed5290");

    [Fact]
    public async Task List_returns_direct_paged_summaries_and_forwards_typed_criteria()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListSalesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedSalesResult(
                [new SaleSummaryResult(
                    SaleId,
                    "SALE-001",
                    new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero),
                    new ExternalIdentityResult("CUSTOMER-001", "Customer"),
                    new ExternalIdentityResult("BRANCH-001", "Branch"),
                    36m,
                    false,
                    new DateTimeOffset(2026, 9, 24, 12, 1, 0, TimeSpan.Zero),
                    1)],
                26,
                2,
                2));
        await using var factory = CreateFactory(mediator);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/sales?_page=2&_size=25&_order=totalAmount%20desc&customerExternalId=CUSTOMER-001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(26, body.RootElement.GetProperty("totalItems").GetInt64());
        Assert.Equal(2, body.RootElement.GetProperty("currentPage").GetInt32());
        var summary = Assert.Single(body.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal(SaleId, summary.GetProperty("id").GetGuid());
        Assert.False(summary.TryGetProperty("items", out _));
        await mediator.Received(1).Send(
            Arg.Is<ListSalesQuery>(query =>
                query.Criteria.Page == 2 &&
                query.Criteria.PageSize == 25 &&
                query.Criteria.CustomerExternalIds.Single() == "CUSTOMER-001" &&
                query.Criteria.Order.First().Field == SaleOrderField.TotalAmount &&
                query.Criteria.Order.Last().Field == SaleOrderField.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_with_unknown_parameter_returns_validation_contract_without_dispatching()
    {
        var mediator = Substitute.For<IMediator>();
        await using var factory = CreateFactory(mediator);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/sales?sort=DROP%20TABLE%20Sales");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemType(response, "ValidationError");
        await mediator.DidNotReceive().Send(Arg.Any<ListSalesQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_returns_direct_resource_location_and_etag()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<CreateSaleCommand>(), Arg.Any<CancellationToken>()).Returns(Result());
        await using var factory = CreateFactory(mediator);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/sales", new
        {
            saleNumber = "SALE-001",
            saleDate = "2026-09-24T12:00:00Z",
            customer = new { externalId = "CUSTOMER-001", name = "Customer" },
            branch = new { externalId = "BRANCH-001", name = "Branch" },
            items = new[]
            {
                new
                {
                    product = new { externalId = "PRODUCT-001", name = "Product" },
                    quantity = 4,
                    unitPrice = 10m
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/sales/{SaleId}", response.Headers.Location?.OriginalString);
        Assert.Equal("\"sale-1\"", response.Headers.ETag?.Tag);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(SaleId, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(36m, body.RootElement.GetProperty("totalAmount").GetDecimal());
        Assert.False(body.RootElement.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task Get_returns_complete_resource_and_etag()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetSaleQuery>(), Arg.Any<CancellationToken>()).Returns(Result());
        await using var factory = CreateFactory(mediator);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/sales/{SaleId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"sale-1\"", response.Headers.ETag?.Tag);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(1, body.RootElement.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Put_forwards_etag_version_and_returns_updated_etag()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateSaleCommand>(), Arg.Any<CancellationToken>()).Returns(Result(2));
        await using var factory = CreateFactory(mediator);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/sales/{SaleId}");
        request.Headers.TryAddWithoutValidation("If-Match", "\"sale-1\"");
        request.Content = JsonContent.Create(UpdateBody());

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"sale-2\"", response.Headers.ETag?.Tag);
        await mediator.Received(1).Send(
            Arg.Is<UpdateSaleCommand>(command => command.Id == SaleId && command.ExpectedVersion == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Put_without_if_match_returns_428_contract_without_dispatching()
    {
        var mediator = Substitute.For<IMediator>();
        await using var factory = CreateFactory(mediator);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync($"/api/sales/{SaleId}", UpdateBody());

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        await AssertProblemType(response, "PreconditionRequired");
        await mediator.DidNotReceive().Send(Arg.Any<UpdateSaleCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Put_with_stale_etag_returns_412_contract()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpdateSaleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SaleResult>(new DomainConcurrencyException(
                DomainErrorCodes.Sale.VersionConflict,
                "The sale changed after it was retrieved.")));
        await using var factory = CreateFactory(mediator);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/sales/{SaleId}");
        request.Headers.TryAddWithoutValidation("If-Match", "\"sale-1\"");
        request.Content = JsonContent.Create(UpdateBody());

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        await AssertProblemType(response, "ConcurrencyConflict");
    }

    [Theory]
    [InlineData("*")]
    [InlineData("W/\"sale-1\"")]
    [InlineData("\"sale-1\", \"sale-2\"")]
    public async Task Put_with_unsupported_if_match_returns_400(string etag)
    {
        var mediator = Substitute.For<IMediator>();
        await using var factory = CreateFactory(mediator);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/sales/{SaleId}");
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        request.Content = JsonContent.Create(UpdateBody());

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemType(response, "InvalidRequest");
        await mediator.DidNotReceive().Send(Arg.Any<UpdateSaleCommand>(), Arg.Any<CancellationToken>());
    }

    private static object UpdateBody() => new
    {
        saleDate = "2026-09-24T12:00:00Z",
        customer = new { externalId = "CUSTOMER-001", name = "Customer" },
        branch = new { externalId = "BRANCH-001", name = "Branch" },
        items = new[]
        {
            new
            {
                id = "57483932-d678-473a-a890-f91877cece36",
                product = new { externalId = "PRODUCT-001", name = "Product" },
                quantity = 4,
                unitPrice = 10m
            }
        }
    };

    private static SaleResult Result(long version = 1) => new(
        SaleId,
        "SALE-001",
        new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero),
        new ExternalIdentityResult("CUSTOMER-001", "Customer"),
        new ExternalIdentityResult("BRANCH-001", "Branch"),
        [new SaleItemResult(
            Guid.Parse("57483932-d678-473a-a890-f91877cece36"),
            new ExternalIdentityResult("PRODUCT-001", "Product"),
            4,
            10m,
            0.10m,
            40m,
            4m,
            36m,
            36m,
            false,
            null)],
        36m,
        false,
        null,
        new DateTimeOffset(2026, 9, 24, 12, 0, 1, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 24, 12, 0, 1, TimeSpan.Zero),
        version);

    private static WebApplicationFactory<Program> CreateFactory(IMediator mediator) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SecretKey"] = "sales-functional-tests-signing-key-at-least-32-bytes",
                    ["Jwt:Issuer"] = "SalesFunctionalTests",
                    ["Jwt:Audience"] = "SalesFunctionalTests.Client",
                    ["Jwt:ExpirationMinutes"] = "60"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMediator>();
                services.AddSingleton(mediator);
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.SchemeName,
                        _ => { });
            });
        });

    private static async Task AssertProblemType(HttpResponseMessage response, string type)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(type, body.RootElement.GetProperty("type").GetString());
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "SalesFunctionalTests";

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Manager")
            ], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
