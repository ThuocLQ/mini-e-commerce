using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProjectionWorker.Application.Abstractions;
using ProjectionWorker.Infrastructure.MongoDb;
using Testcontainers.MongoDb;

namespace MicroShop.IntegrationTests.Projection;

public sealed class ProcessedProjectionEventStoreTests
{
    [Fact]
    public async Task MarkingSameEventTwice_StoresSingleProcessedMarker()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var mongodb = new MongoDbBuilder("mongo:7").Build();
        await mongodb.StartAsync(cancellationToken);

        var options = new MongoDbOptions
        {
            ConnectionString = mongodb.GetConnectionString(),
            DatabaseName = $"MicroShop_OrderReadDb_{Guid.NewGuid():N}",
            ProcessedEventsCollectionName = "processed_projection_events"
        };
        var client = new MongoClient(options.ConnectionString);
        var store = new MongoProcessedProjectionEventStore(client, Options.Create(options));
        var processedEvent = new ProcessedProjectionEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            EventType = "OrderCreated",
            OccurredAtUtc = DateTime.UtcNow
        };

        Assert.False(await store.IsProcessedAsync(processedEvent.EventId, cancellationToken));

        await store.MarkProcessedAsync(processedEvent, cancellationToken);
        await store.MarkProcessedAsync(processedEvent, cancellationToken);

        Assert.True(await store.IsProcessedAsync(processedEvent.EventId, cancellationToken));

        var count = await client
            .GetDatabase(options.DatabaseName)
            .GetCollection<ProcessedProjectionEventDocument>(options.ProcessedEventsCollectionName)
            .CountDocumentsAsync(
                document => document.EventId == processedEvent.EventId.ToString("D"),
                cancellationToken: cancellationToken);

        Assert.Equal(1, count);
    }
}
