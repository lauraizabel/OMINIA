using Ambev.DeveloperEvaluation.WebApi.Middleware;
using Ambev.DeveloperEvaluation.WebApi.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Security;

public sealed class TelemetryConfigurationTests
{
    [Theory]
    [InlineData("Telemetry:ServiceName", "")]
    [InlineData("Telemetry:OtlpEndpoint", "relative/path")]
    [InlineData("Telemetry:OtlpEndpoint", "ftp://collector:4317")]
    public void Enabled_telemetry_rejects_invalid_configuration(string key, string value)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:Enabled"] = "true",
            [key] = value
        });
        builder.AddApplicationTelemetry();
        using var services = builder.Services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => services.GetRequiredService<IOptions<TelemetryOptions>>().Value);
    }

    [Fact]
    public void Disabled_telemetry_keeps_safe_defaults_without_an_exporter()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:Enabled"] = "false",
            ["Telemetry:OtlpEndpoint"] = "not-a-uri"
        });
        builder.AddApplicationTelemetry();
        using var services = builder.Services.BuildServiceProvider();

        Assert.False(services.GetRequiredService<IOptions<TelemetryOptions>>().Value.Enabled);
    }

    [Fact]
    public async Task Exception_middleware_exposes_the_distributed_trace_id_as_the_api_correlation_id()
    {
        using var activity = new Activity("incoming").Start();
        var context = new DefaultHttpContext();
        var middleware = new ApiExceptionMiddleware(
            _ => Task.CompletedTask,
            NullLogger<ApiExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(activity.TraceId.ToString(), context.TraceIdentifier);
    }
}
