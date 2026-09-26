using System.Text.Json;
using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Ambev.DeveloperEvaluation.ORM.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Persistence;

[Collection(MongoDbCollection.Name)]
public sealed class MongoSaleAuditTests
{
    private readonly MongoDbFixture _database;

    public MongoSaleAuditTests(MongoDbFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Publishing_the_same_event_twice_is_idempotent()
    {
        var databaseName = $"audit_{Guid.NewGuid():N}";
        var options = Options.Create(new MongoAuditOptions
        {
            Enabled = true,
            ConnectionString = _database.ConnectionString,
            DatabaseName = databaseName,
            CollectionName = "sale_events"
        });
        var client = new MongoClient(_database.ConnectionString);
        var publisher = new MongoSaleDomainEventPublisher(
            client,
            options,
            NullLogger<MongoSaleDomainEventPublisher>.Instance);
        var message = Message(version: 1);

        await Task.WhenAll(
            publisher.PublishAsync(message),
            publisher.PublishAsync(message),
            publisher.PublishAsync(message));

        var documents = client.GetDatabase(databaseName)
            .GetCollection<SaleAuditDocument>("sale_events");
        var persisted = await documents.Find(Builders<SaleAuditDocument>.Filter.Empty).ToListAsync();

        var audit = Assert.Single(persisted);
        Assert.Equal(message.EventId.ToString("D"), audit.EventId);
        Assert.Equal(message.SaleId.ToString("D"), audit.SaleId);
        Assert.Equal(message.Version, audit.Version);
        Assert.Equal(message.EventType, audit.EventType);
        Assert.Equal(message.CorrelationId, audit.CorrelationId);
        Assert.Equal(message.SaleId.ToString(), audit.Payload["SaleId"].AsString);
    }

    [Fact]
    public async Task Audit_documents_can_be_read_in_aggregate_version_order()
    {
        var databaseName = $"audit_{Guid.NewGuid():N}";
        var options = Options.Create(new MongoAuditOptions
        {
            Enabled = true,
            ConnectionString = _database.ConnectionString,
            DatabaseName = databaseName,
            CollectionName = "sale_events"
        });
        var client = new MongoClient(_database.ConnectionString);
        var publisher = new MongoSaleDomainEventPublisher(
            client,
            options,
            NullLogger<MongoSaleDomainEventPublisher>.Instance);
        var saleId = Guid.NewGuid();

        await publisher.PublishAsync(Message(version: 2, saleId));
        await publisher.PublishAsync(Message(version: 1, saleId));

        var documents = client.GetDatabase(databaseName)
            .GetCollection<SaleAuditDocument>("sale_events");
        var persisted = await documents
            .Find(document => document.SaleId == saleId.ToString("D"))
            .SortBy(document => document.Version)
            .ToListAsync();

        Assert.Equal([1L, 2L], persisted.Select(document => document.Version));
    }

    [Fact]
    public async Task Mongo_health_is_healthy_when_available_and_degraded_when_unavailable()
    {
        var available = new MongoAuditHealthCheck(new MongoClient(_database.ConnectionString));
        Assert.Equal(
            HealthStatus.Healthy,
            (await available.CheckHealthAsync(new HealthCheckContext())).Status);

        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:1");
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(100);
        var unavailable = new MongoAuditHealthCheck(new MongoClient(settings));
        Assert.Equal(
            HealthStatus.Degraded,
            (await unavailable.CheckHealthAsync(new HealthCheckContext())).Status);
    }

    private static SaleEventMessage Message(long version, Guid? saleId = null)
    {
        var id = saleId ?? Guid.NewGuid();
        return new SaleEventMessage(
            Guid.NewGuid(),
            "SaleModifiedEvent",
            id,
            version,
            new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero),
            "integration-correlation",
            JsonSerializer.Serialize(new { SaleId = id, Version = version }));
    }
}
