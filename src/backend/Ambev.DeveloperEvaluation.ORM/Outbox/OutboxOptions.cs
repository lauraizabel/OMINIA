namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; init; } = 50;
    public int PollIntervalSeconds { get; init; } = 2;
    public int LeaseSeconds { get; init; } = 30;
    public int MaxAttempts { get; init; } = 10;
    public int BaseRetrySeconds { get; init; } = 2;
    public int MaxRetrySeconds { get; init; } = 300;
    public int DegradedBacklogAgeSeconds { get; init; } = 60;
    public int ProcessedRetentionDays { get; init; } = 7;
    public int CleanupIntervalMinutes { get; init; } = 60;
}
