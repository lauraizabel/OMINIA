namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public sealed class MongoAuditOptions
{
    public const string SectionName = "MongoAudit";

    public bool Enabled { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = "developer_evaluation_audit";
    public string CollectionName { get; init; } = "sale_events";
}
