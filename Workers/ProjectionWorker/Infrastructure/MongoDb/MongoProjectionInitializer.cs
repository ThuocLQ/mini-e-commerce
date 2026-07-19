using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace ProjectionWorker.Infrastructure.MongoDb;

public sealed class MongoProjectionInitializer : IMongoProjectionInitializer
{
    private readonly IMongoClient _mongoClient;
    private readonly MongoDbOptions _options;

    public MongoProjectionInitializer(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options)
    {
        _mongoClient = mongoClient;
        _options = options.Value;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= _options.InitializeMaxRetryCount; attempt++)
        {
            try
            {
                await CreateIndexesAsync(cancellationToken);
                return;
            }
            catch when (attempt < _options.InitializeMaxRetryCount)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.InitializeRetryDelaySeconds),
                    cancellationToken);
            }
        }

        await CreateIndexesAsync(cancellationToken);
    }

    private async Task CreateIndexesAsync(CancellationToken cancellationToken)
    {
        var database = _mongoClient.GetDatabase(_options.DatabaseName);

        var orderSummaries = database.GetCollection<OrderSummaryProjectionDocument>(
            _options.EffectiveOrderSummariesCollectionName);

        await orderSummaries.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<OrderSummaryProjectionDocument>(
                    Builders<OrderSummaryProjectionDocument>.IndexKeys.Ascending(x => x.OrderId),
                    new CreateIndexOptions
                    {
                        Name = "UX_order_summaries_orderId",
                        Unique = true
                    }),
                new CreateIndexModel<OrderSummaryProjectionDocument>(
                    Builders<OrderSummaryProjectionDocument>.IndexKeys.Descending(x => x.CreatedAtUtc),
                    new CreateIndexOptions
                    {
                        Name = "IX_order_summaries_createdAtUtc_desc"
                    }),
                new CreateIndexModel<OrderSummaryProjectionDocument>(
                    Builders<OrderSummaryProjectionDocument>.IndexKeys
                        .Ascending(x => x.CustomerId)
                        .Descending(x => x.CreatedAtUtc),
                    new CreateIndexOptions
                    {
                        Name = "IX_order_summaries_customerId_createdAtUtc_desc"
                    })
            ],
            cancellationToken);

        var failures = database.GetCollection<ProjectionFailureDocument>(
            _options.ProjectionFailuresCollectionName);

        await failures.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<ProjectionFailureDocument>(
                    Builders<ProjectionFailureDocument>.IndexKeys.Ascending(x => x.EventId),
                    new CreateIndexOptions
                    {
                        Name = "IX_projection_failures_eventId"
                    }),
                new CreateIndexModel<ProjectionFailureDocument>(
                    Builders<ProjectionFailureDocument>.IndexKeys.Descending(x => x.CreatedAtUtc),
                    new CreateIndexOptions
                    {
                        Name = "IX_projection_failures_createdAtUtc_desc"
                    }),
                new CreateIndexModel<ProjectionFailureDocument>(
                    Builders<ProjectionFailureDocument>.IndexKeys
                        .Ascending(x => x.Topic)
                        .Ascending(x => x.Partition)
                        .Ascending(x => x.Offset),
                    new CreateIndexOptions
                    {
                        Name = "UX_projection_failures_topic_partition_offset",
                        Unique = true
                    })
            ],
            cancellationToken);

        var processedEvents = database.GetCollection<ProcessedProjectionEventDocument>(
            _options.ProcessedEventsCollectionName);

        await processedEvents.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<ProcessedProjectionEventDocument>(
                    Builders<ProcessedProjectionEventDocument>.IndexKeys
                        .Ascending(x => x.OrderId)
                        .Descending(x => x.OccurredAtUtc),
                    new CreateIndexOptions
                    {
                        Name = "IX_processed_projection_events_orderId_occurredAtUtc_desc"
                    }),
                new CreateIndexModel<ProcessedProjectionEventDocument>(
                    Builders<ProcessedProjectionEventDocument>.IndexKeys.Descending(x => x.ProcessedAtUtc),
                    new CreateIndexOptions
                    {
                        Name = "IX_processed_projection_events_processedAtUtc_desc"
                    })
            ],
            cancellationToken);
    }
}
