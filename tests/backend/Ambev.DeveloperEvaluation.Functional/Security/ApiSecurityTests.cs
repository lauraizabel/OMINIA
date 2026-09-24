using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.WebApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Security;

public sealed class ApiSecurityTests
{
    private const string Issuer = "FunctionalTests";
    private const string Audience = "FunctionalTests.Client";
    private const string SigningKey = "functional-tests-signing-key-at-least-32-bytes-long";

    [Fact]
    public async Task Protected_endpoint_without_token_returns_contract_401()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized,
            await response.Content.ReadAsStringAsync());
        await AssertErrorContractAsync(response, "AuthenticationError");
    }

    [Fact]
    public async Task Customer_token_cannot_access_administrator_endpoint()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("Customer"));

        using var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertErrorContractAsync(response, "AuthorizationError");
    }

    [Fact]
    public async Task Token_is_rejected_when_user_was_suspended_after_login()
    {
        await using var factory = CreateFactory(isActive: false);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("Admin"));

        using var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertErrorContractAsync(response, "AuthenticationError");
    }

    [Theory]
    [InlineData("wrong-issuer", Audience, 5)]
    [InlineData(Issuer, "wrong-audience", 5)]
    [InlineData(Issuer, Audience, -5)]
    public async Task Invalid_token_is_rejected(string issuer, string audience, int expiresInMinutes)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("Admin", issuer, audience, expiresInMinutes));

        using var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertErrorContractAsync(response, "AuthenticationError");
    }

    [Fact]
    public async Task Malformed_json_returns_validation_contract()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{\"email\":", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/auth", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorContractAsync(response, "ValidationError", expectErrors: true);
    }

    [Fact]
    public async Task Unknown_json_property_is_rejected()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(
            """{"email":"admin@example.com","password":"Password1!","isAdmin":true}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/auth", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorContractAsync(response, "ValidationError", expectErrors: true);
    }

    [Fact]
    public async Task Unsupported_content_type_returns_415_contract()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("email=admin@example.com&password=Password1!", Encoding.UTF8, "text/plain");

        using var response = await client.PostAsync("/api/auth", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        await AssertErrorContractAsync(response, "UnsupportedMediaType");
    }

    [Fact]
    public async Task Oversized_request_returns_413_contract()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(new string('x', 257 * 1024), Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/auth", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        await AssertErrorContractAsync(response, "PayloadTooLarge");
    }

    [Fact]
    public async Task Sixth_login_attempt_from_same_address_is_rate_limited()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/api/auth", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var finalContent = new StringContent("{}", Encoding.UTF8, "application/json");
        using var limitedResponse = await client.PostAsync("/api/auth", finalContent);

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        await AssertErrorContractAsync(limitedResponse, "RateLimitExceeded");
    }

    [Theory]
    [InlineData("https://allowed.example", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task Cors_allows_only_configured_origins(string origin, bool shouldBeAllowed)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        using var response = await client.SendAsync(request);

        var hasAllowOrigin = response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins);
        Assert.Equal(shouldBeAllowed, hasAllowOrigin && origins?.Contains(origin) == true);
    }

    private static WebApplicationFactory<Program> CreateFactory(bool isActive = true)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SecretKey"] = SigningKey,
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:ExpirationMinutes"] = "60",
                    ["Cors:AllowedOrigins:0"] = "https://allowed.example"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuthenticatedUserStatusValidator>();
                services.AddSingleton<IAuthenticatedUserStatusValidator>(
                    new ConfigurableUserStatusValidator(isActive));
            });
        });
    }

    private static string CreateToken(
        string role,
        string issuer = Issuer,
        string audience = Audience,
        int expiresInMinutes = 5)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, "Functional Test User"),
                new Claim(ClaimTypes.Role, role)
            ]),
            NotBefore = DateTime.UtcNow.AddMinutes(-10),
            Expires = DateTime.UtcNow.AddMinutes(expiresInMinutes),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static async Task AssertErrorContractAsync(
        HttpResponseMessage response,
        string expectedType,
        bool expectErrors = false)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(stream);
        var root = json.RootElement;

        Assert.Equal(expectedType, root.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("error").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));

        if (expectErrors)
        {
            Assert.True(root.GetProperty("errors").GetArrayLength() > 0);
        }
    }

    private sealed class ConfigurableUserStatusValidator : IAuthenticatedUserStatusValidator
    {
        private readonly bool _isActive;

        public ConfigurableUserStatusValidator(bool isActive)
        {
            _isActive = isActive;
        }

        public Task<bool> IsActiveAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(_isActive);
    }
}
