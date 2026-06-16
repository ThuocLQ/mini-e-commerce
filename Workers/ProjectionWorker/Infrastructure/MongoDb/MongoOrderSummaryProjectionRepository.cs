using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProjectionWorker.Application.Abstractions;
using ProjectionWorker.Application.Events;

namespace ProjectionWorker.Infrastructure.MongoDb;

public sealed class MongoOrderSummaryProjectionRepository : IOrderSummaryProjectionRepository
{
    private readonly IMongoCollection<OrderSummaryProjectionDocument> _collection;

    public MongoOrderSummaryProjectionRepository(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options)
    {
        var mongoOptions = options.Value;
        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);

        _collection = database.GetCollection<OrderSummaryProjectionDocument>(
            mongoOptions.EffectiveOrderSummariesCollectionName);
    }

    public async Task ApplyAsync(
        OrderProjectionEvent orderEvent,
        CancellationToken cancellationToken = default)
    {
        var id = orderEvent.OrderId.ToString("D");
        var filter = Builders<OrderSummaryProjectionDocument>.Filter.Eq(x => x.Id, id);

        var existing = await _collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);

        if (IsOlderThanCurrentProjection(orderEvent, existing))
        {
            return;
        }

        var document = orderEvent.EventType switch
        {
            OrderProjectionEventTypes.OrderCreated => ApplyOrderCreated(orderEvent, existing),
            OrderProjectionEventTypes.OrderPaid => ApplyOrderPaid(orderEvent, existing),
            OrderProjectionEventTypes.OrderCancelled => ApplyOrderCancelled(orderEvent, existing),
            _ => throw new ArgumentException($"Unsupported order event type '{orderEvent.EventType}'.")
        };

        await _collection.ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    private static bool IsOlderThanCurrentProjection(
        OrderProjectionEvent orderEvent,
        OrderSummaryProjectionDocument? existing)
    {
        return existing?.LastProjectedEventOccurredAtUtc is { } lastProjectedAt
            && orderEvent.OccurredAtUtc < lastProjectedAt;
    }

    private static OrderSummaryProjectionDocument ApplyOrderCreated(
        OrderProjectionEvent orderEvent,
        OrderSummaryProjectionDocument? existing)
    {
        var status = existing is null
            ? "Created"
            : PreserveTerminalStatus(existing.Status);

        return BuildDocument(
            orderEvent,
            existing,
            status,
            paidAtUtc: existing?.PaidAtUtc,
            cancelledAtUtc: existing?.CancelledAtUtc);
    }

    private static OrderSummaryProjectionDocument ApplyOrderPaid(
        OrderProjectionEvent orderEvent,
        OrderSummaryProjectionDocument? existing)
    {
        return BuildDocument(
            orderEvent,
            existing,
            status: "Paid",
            paidAtUtc: orderEvent.OccurredAtUtc,
            cancelledAtUtc: existing?.CancelledAtUtc);
    }

    private static OrderSummaryProjectionDocument ApplyOrderCancelled(
        OrderProjectionEvent orderEvent,
        OrderSummaryProjectionDocument? existing)
    {
        return BuildDocument(
            orderEvent,
            existing,
            status: "Cancelled",
            paidAtUtc: existing?.PaidAtUtc,
            cancelledAtUtc: orderEvent.OccurredAtUtc);
    }

    private static OrderSummaryProjectionDocument BuildDocument(
        OrderProjectionEvent orderEvent,
        OrderSummaryProjectionDocument? existing,
        string status,
        DateTime? paidAtUtc,
        DateTime? cancelledAtUtc)
    {
        var items = orderEvent.Items.Count > 0
            ? orderEvent.Items.Select(item => new OrderSummaryProjectionItemDocument
            {
                ProductId = item.ProductId.ToString("D"),
                ProductName = item.ProductName.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList()
            : existing?.Items ?? [];

        return new OrderSummaryProjectionDocument
        {
            Id = orderEvent.OrderId.ToString("D"),
            OrderId = orderEvent.OrderId.ToString("D"),
            CustomerId = orderEvent.CustomerId.ToString("D"),
            CustomerName = orderEvent.CustomerName.Trim(),
            Status = status,
            TotalAmount = orderEvent.TotalAmount,
            Currency = orderEvent.Currency.Trim().ToUpperInvariant(),
            ItemCount = orderEvent.ItemCount > 0 ? orderEvent.ItemCount : items.Count,
            Items = items,
            CreatedAtUtc = existing?.CreatedAtUtc ?? orderEvent.OccurredAtUtc,
            LastUpdatedAtUtc = orderEvent.OccurredAtUtc,
            PaidAtUtc = paidAtUtc,
            CancelledAtUtc = cancelledAtUtc,
            LastProjectedEventId = orderEvent.EventId.ToString("D"),
            LastProjectedEventType = orderEvent.EventType,
            LastProjectedEventOccurredAtUtc = orderEvent.OccurredAtUtc,
            LastProjectedAtUtc = DateTime.UtcNow
        };
    }

    private static string PreserveTerminalStatus(string status)
    {
        return status is "Paid" or "Cancelled"
            ? status
            : "Created";
    }
}
