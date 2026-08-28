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
        var idFilter = Builders<OrderSummaryProjectionDocument>.Filter.Eq(x => x.Id, id);

        var existing = await _collection
            .Find(idFilter)
            .FirstOrDefaultAsync(cancellationToken);

        if (IsOlderThanCurrentProjection(orderEvent, existing))
        {
            return;
        }

        var document = orderEvent.EventType switch
        {
            OrderProjectionEventTypes.OrderCreated => ApplyOrderCreated(orderEvent, existing),
            OrderProjectionEventTypes.OrderPaid => ApplyOrderPaid(orderEvent, existing),
            OrderProjectionEventTypes.OrderConfirmed => ApplyOrderFulfillmentStatus(orderEvent, existing, "Confirmed"),
            OrderProjectionEventTypes.OrderShipped => ApplyOrderFulfillmentStatus(orderEvent, existing, "Shipped"),
            OrderProjectionEventTypes.OrderDelivered => ApplyOrderFulfillmentStatus(orderEvent, existing, "Delivered"),
            OrderProjectionEventTypes.OrderRefunded => ApplyOrderRefunded(orderEvent, existing),
            OrderProjectionEventTypes.OrderPaymentFailed => ApplyOrderPaymentFailed(orderEvent, existing),
            OrderProjectionEventTypes.OrderCancelled => ApplyOrderCancelled(orderEvent, existing),
            _ => throw new ArgumentException($"Unsupported order event type '{orderEvent.EventType}'.")
        };

        var versionFilter = Builders<OrderSummaryProjectionDocument>.Filter.Or(
            Builders<OrderSummaryProjectionDocument>.Filter.Exists(x => x.LastProjectedEventSequence, false),
            Builders<OrderSummaryProjectionDocument>.Filter.Lt(x => x.LastProjectedEventSequence, orderEvent.Sequence));

        try
        {
            await _collection.ReplaceOneAsync(
                Builders<OrderSummaryProjectionDocument>.Filter.And(idFilter, versionFilter),
                document,
                new ReplaceOptions { IsUpsert = existing is null },
                cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // A competing delivery inserted the same aggregate first. Its sequence is at least
            // as recent as this event, so this delivery can safely be acknowledged.
        }
    }

    private static bool IsOlderThanCurrentProjection(
        OrderProjectionEvent orderEvent,
        OrderSummaryProjectionDocument? existing)
    {
        return existing?.LastProjectedEventSequence is { } lastSequence
            ? orderEvent.Sequence <= lastSequence
            : existing?.LastProjectedEventOccurredAtUtc is { } lastProjectedAt
              && orderEvent.OccurredAtUtc <= lastProjectedAt;
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

    private static OrderSummaryProjectionDocument ApplyOrderFulfillmentStatus(
        OrderProjectionEvent orderEvent,
        OrderSummaryProjectionDocument? existing,
        string status)
    {
        return BuildDocument(
            orderEvent,
            existing,
            status,
            paidAtUtc: existing?.PaidAtUtc ?? orderEvent.OccurredAtUtc,
            cancelledAtUtc: existing?.CancelledAtUtc);
    }

    private static OrderSummaryProjectionDocument ApplyOrderRefunded(
        OrderProjectionEvent orderEvent,
        OrderSummaryProjectionDocument? existing)
    {
        return BuildDocument(
            orderEvent,
            existing,
            status: "Refunded",
            paidAtUtc: existing?.PaidAtUtc,
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

    private static OrderSummaryProjectionDocument ApplyOrderPaymentFailed(
        OrderProjectionEvent orderEvent,
        OrderSummaryProjectionDocument? existing)
    {
        return BuildDocument(
            orderEvent,
            existing,
            status: "PaymentFailed",
            paidAtUtc: existing?.PaidAtUtc,
            cancelledAtUtc: existing?.CancelledAtUtc);
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
            LastProjectedEventSequence = orderEvent.Sequence,
            LastProjectedAtUtc = DateTime.UtcNow
        };
    }

    private static string PreserveTerminalStatus(string status)
    {
        return status is "Paid" or "Confirmed" or "Shipped" or "Delivered" or "Refunded" or "Cancelled"
            ? status
            : "Created";
    }
}
