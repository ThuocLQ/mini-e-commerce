using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProjectionWorker.Application.Abstractions;

namespace ProjectionWorker.Infrastructure.MongoDb;

public sealed class MongoProcessedProjectionEventStore : IProcessedProjectionEventStore
{
    private readonly IMongoCollection<ProcessedProjectionEventDocument> _collection;

    public MongoProcessedProjectionEventStore(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options)
    {
        var mongoOptions = options.Value;
        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);

        _collection = database.GetCollection<ProcessedProjectionEventDocument>(
            mongoOptions.ProcessedEventsCollectionName);
    }

    public async Task<bool> IsProcessedAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var id = eventId.ToString("D");
        return await _collection
            .Find(document => document.Id == id)
            .AnyAsync(cancellationToken);
    }

    public async Task MarkProcessedAsync(
        ProcessedProjectionEvent processedEvent,
        CancellationToken cancellationToken = default)
    {
        var id = processedEvent.EventId.ToString("D");
        var document = new ProcessedProjectionEventDocument
        {
            Id = id,
            EventId = id,
            OrderId = processedEvent.OrderId.ToString("D"),
            EventType = processedEvent.EventType,
            OccurredAtUtc = processedEvent.OccurredAtUtc,
            ProcessedAtUtc = processedEvent.ProcessedAtUtc
        };

        await _collection.ReplaceOneAsync(
            item => item.Id == id,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}
