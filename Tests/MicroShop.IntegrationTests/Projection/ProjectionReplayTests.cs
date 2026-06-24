using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProjectionWorker.Application.Events;
using ProjectionWorker.Infrastructure.MongoDb;
using Testcontainers.MongoDb;

namespace MicroShop.IntegrationTests.Projection;

public sealed class ProjectionReplayTests
{
    [Fact]
    public async Task ReplayingSameOrderEvent_UpsertsSingleReadModel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var mongodb = new MongoDbBuilder("mongo:7").Build();

        await mongodb.StartAsync(cancellationToken);

        var databaseName = $"MicroShop_OrderReadDb_{Guid.NewGuid():N}";
        var options = new MongoDbOptions
        {
            ConnectionString = mongodb.GetConnectionString(),
            DatabaseName = databaseName,
            OrderSummariesCollectionName = "order_summaries",
            ProjectionFailuresCollectionName = "projection_failures"
        };

        var client = new MongoClient(options.ConnectionString);
        var repository = new MongoOrderSummaryProjectionRepository(
            client,
            Options.Create(options));

        var orderEvent = new OrderProjectionEvent
        {
            EventId = Guid.NewGuid(),
            EventType = OrderProjectionEventTypes.OrderCreated,
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Replay Customer",
            TotalAmount = 250_000m,
            Currency = "VND",
            ItemCount = 1,
            Items =
            [
                new OrderProjectionItem
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "Test Product",
                    Quantity = 1,
                    UnitPrice = 250_000m
                }
            ],
            OccurredAtUtc = DateTime.UtcNow
        };

        await repository.ApplyAsync(orderEvent, cancellationToken);
        await repository.ApplyAsync(orderEvent, cancellationToken);

        var collection = client
            .GetDatabase(databaseName)
            .GetCollection<OrderSummaryProjectionDocument>("order_summaries");
        var documents = await collection
            .Find(document => document.OrderId == orderEvent.OrderId.ToString("D"))
            .ToListAsync(cancellationToken);

        var document = Assert.Single(documents);
        Assert.Equal(orderEvent.EventId.ToString("D"), document.LastProjectedEventId);
        Assert.Equal("Created", document.Status);
    }
}
