using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ambev.DeveloperEvaluation.ORM.HealthChecks;

public sealed class MongoAuditHealthCheck : IHealthCheck
{
    private readonly IMongoClient _client;

    public MongoAuditHealthCheck(IMongoClient client)
    {
        _client = client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("MongoDB audit storage is available.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Degraded("MongoDB audit storage is unavailable; events remain in the outbox.", exception);
        }
    }
}
