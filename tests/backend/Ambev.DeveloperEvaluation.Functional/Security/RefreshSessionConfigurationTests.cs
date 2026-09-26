using Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;
using Ambev.DeveloperEvaluation.IoC.ModuleInitializers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Security;

public sealed class RefreshSessionConfigurationTests
{
    [Theory]
    [InlineData("RefreshSession:IdleExpirationDays", "0")]
    [InlineData("RefreshSession:IdleExpirationDays", "31")]
    [InlineData("RefreshSession:AbsoluteExpirationDays", "0")]
    [InlineData("RefreshSession:AbsoluteExpirationDays", "91")]
    [InlineData("RefreshSession:AbsoluteExpirationDays", "6")]
    [InlineData("RefreshSession:TokenSizeBytes", "31")]
    [InlineData("RefreshSession:TokenSizeBytes", "129")]
    [InlineData("RefreshSession:ReuseGraceSeconds", "-1")]
    [InlineData("RefreshSession:ReuseGraceSeconds", "61")]
    [InlineData("RefreshSession:CookieName", "")]
    [InlineData("RefreshSession:CookieName", "refresh-without-host-prefix")]
    public void Invalid_refresh_session_configuration_fails_validation(string key, string value)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [key] = value
        });
        new InfrastructureModuleInitializer().Initialize(builder);
        using var services = builder.Services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => services.GetRequiredService<IOptions<RefreshSessionOptions>>().Value);
    }

    [Fact]
    public void Insecure_development_cookie_does_not_require_the_host_prefix()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RefreshSession:CookieName"] = "development-refresh",
            ["RefreshSession:SecureCookie"] = "false"
        });
        new InfrastructureModuleInitializer().Initialize(builder);
        using var services = builder.Services.BuildServiceProvider();

        Assert.Equal(
            "development-refresh",
            services.GetRequiredService<IOptions<RefreshSessionOptions>>().Value.CookieName);
    }
}
