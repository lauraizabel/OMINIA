using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlReadinessHealthCheckTests
{
    private readonly PostgreSqlFixture _database;

    public PostgreSqlReadinessHealthCheckTests(PostgreSqlFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Readiness_ShouldBeHealthyWhenPostgreSqlIsAvailable()
    {
        await using var context = CreateContext(_database.ConnectionString);
        var result = await new PostgreSqlReadinessHealthCheck(context)
            .CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Readiness_ShouldBeUnhealthyWhenPostgreSqlIsUnavailable()
    {
        const string unavailableConnection =
            "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=test;Timeout=1;Pooling=false";
        await using var context = CreateContext(unavailableConnection);

        var result = await new PostgreSqlReadinessHealthCheck(context)
            .CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private static DefaultContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DefaultContext(options);
    }
}
