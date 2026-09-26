using Ambev.DeveloperEvaluation.Application.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public sealed class MongoSaleDomainEventPublisher : ISaleDomainEventPublisher
{
    private readonly IMongoCollection<SaleAuditDocument> _collection;
    private readonly ILogger<MongoSaleDomainEventPublisher> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public MongoSaleDomainEventPublisher(
        IMongoClient client,
        IOptions<MongoAuditOptions> options,
        ILogger<MongoSaleDomainEventPublisher> logger)
    {
        var configuration = options.Value;
        _collection = client
            .GetDatabase(configuration.DatabaseName)
            .GetCollection<SaleAuditDocument>(configuration.CollectionName);
        _logger = logger;
    }

    public async Task PublishAsync(
        SaleEventMessage message,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);

        var document = SaleAuditDocument.From(message);
        await _collection.ReplaceOneAsync(
            audit => audit.EventId == document.EventId,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        _logger.LogInformation(
            "Sale audit event persisted. EventId: {EventId}, EventType: {EventType}, SaleId: {SaleId}, Version: {Version}, CorrelationId: {CorrelationId}",
            message.EventId,
            message.EventType,
            message.SaleId,
            message.Version,
            message.CorrelationId);
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
            return;

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
                return;

            var keys = Builders<SaleAuditDocument>.IndexKeys
                .Ascending(document => document.SaleId)
                .Ascending(document => document.Version)
                .Ascending(document => document.OccurredAt);
            await _collection.Indexes.CreateOneAsync(
                new CreateIndexModel<SaleAuditDocument>(keys),
                cancellationToken: cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}

public sealed class SaleAuditDocument
{
    [BsonId]
    public string EventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string SaleId { get; init; } = string.Empty;
    public long Version { get; init; }
    public DateTime OccurredAt { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public BsonDocument Payload { get; init; } = [];

    public static SaleAuditDocument From(SaleEventMessage message) => new()
    {
        EventId = message.EventId.ToString("D"),
        EventType = message.EventType,
        SaleId = message.SaleId.ToString("D"),
        Version = message.Version,
        OccurredAt = message.OccurredAt.UtcDateTime,
        CorrelationId = message.CorrelationId,
        Payload = BsonDocument.Parse(message.Payload)
    };
}
