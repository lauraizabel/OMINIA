namespace Ambev.DeveloperEvaluation.WebApi.Observability;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public bool Enabled { get; init; }
    public string ServiceName { get; init; } = "ambev-sales-api";
    public string OtlpEndpoint { get; init; } = "http://localhost:4317";
}
