using System.Diagnostics;

namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public static class OutboxTelemetry
{
    public const string SourceName = "Ambev.DeveloperEvaluation.Outbox";
    private static readonly ActivitySource Source = new(SourceName);

    public static Activity? StartDelivery(OutboxMessage message)
    {
        var parent = ParentContext(message.CorrelationId);
        return Source.StartActivity(
            "outbox deliver",
            ActivityKind.Consumer,
            parent,
            tags:
            [
                new("messaging.system", "postgresql"),
                new("messaging.operation.name", "deliver"),
                new("messaging.message.id", message.Id.ToString()),
                new("event.type", message.EventType),
                new("sale.id", message.AggregateId.ToString()),
                new("sale.version", message.AggregateVersion)
            ]);
    }

    private static ActivityContext ParentContext(string correlationId)
    {
        if (correlationId.Length == 32 && correlationId.All(Uri.IsHexDigit))
        {
            return new ActivityContext(
                ActivityTraceId.CreateFromString(correlationId.AsSpan()),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded,
                isRemote: true);
        }

        return default;
    }
}
