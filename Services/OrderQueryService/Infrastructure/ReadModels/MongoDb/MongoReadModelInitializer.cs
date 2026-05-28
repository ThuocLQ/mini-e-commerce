using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace OrderQueryService.Infrastructure.ReadModels.MongoDb;

public sealed class MongoReadModelInitializer : IMongoReadModelInitializer
{
    private readonly IMongoClient _mongoClient;
    private readonly MongoDbOptions _options;

    public MongoReadModelInitializer(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options)
    {
        _mongoClient = mongoClient;
        _options = options.Value;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var database = _mongoClient.GetDatabase(_options.DatabaseName);
        var collection = database.GetCollection<OrderSummaryDocument>(
            _options.OrderSummariesCollectionName);

        var orderIdIndex = new CreateIndexModel<OrderSummaryDocument>(
            Builders<OrderSummaryDocument>.IndexKeys.Ascending(x => x.OrderId),
            new CreateIndexOptions
            {
                Name = "UX_order_summaries_orderId",
                Unique = true
            });

        var latestIndex = new CreateIndexModel<OrderSummaryDocument>(
            Builders<OrderSummaryDocument>.IndexKeys.Descending(x => x.CreatedAtUtc),
            new CreateIndexOptions
            {
                Name = "IX_order_summaries_createdAtUtc_desc"
            });

        var customerLatestIndex = new CreateIndexModel<OrderSummaryDocument>(
            Builders<OrderSummaryDocument>.IndexKeys
                .Ascending(x => x.CustomerId)
                .Descending(x => x.CreatedAtUtc),
            new CreateIndexOptions
            {
                Name = "IX_order_summaries_customerId_createdAtUtc_desc"
            });

        await collection.Indexes.CreateManyAsync(
            [orderIdIndex, latestIndex, customerLatestIndex],
            cancellationToken);
    }
}
