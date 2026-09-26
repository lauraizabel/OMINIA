using Ambev.DeveloperEvaluation.ORM.Outbox;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Ambev.DeveloperEvaluation.WebApi.Observability;

public static class TelemetryExtensions
{
    public static WebApplicationBuilder AddApplicationTelemetry(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(TelemetryOptions.SectionName);
        var settings = section.Get<TelemetryOptions>() ?? new TelemetryOptions();

        builder.Services.AddOptions<TelemetryOptions>()
            .Bind(section)
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ServiceName),
                "Telemetry service name is required when tracing is enabled.")
            .Validate(options => !options.Enabled || IsAbsoluteHttpUri(options.OtlpEndpoint),
                "Telemetry OTLP endpoint must be an absolute HTTP or HTTPS URI when tracing is enabled.")
            .ValidateOnStart();

        if (!settings.Enabled)
            return builder;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(settings.ServiceName))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new AlwaysOnSampler()))
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
                    options.RecordException = true;
                })
                .AddHttpClientInstrumentation(options => options.RecordException = true)
                .AddSource("Npgsql", OutboxTelemetry.SourceName)
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(settings.OtlpEndpoint);
                    options.Protocol = OtlpExportProtocol.Grpc;
                }));

        return builder;
    }

    private static bool IsAbsoluteHttpUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
