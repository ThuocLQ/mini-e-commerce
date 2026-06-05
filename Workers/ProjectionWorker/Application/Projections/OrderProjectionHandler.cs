using ProjectionWorker.Application.Abstractions;
using ProjectionWorker.Application.Events;

namespace ProjectionWorker.Application.Projections;

public sealed class OrderProjectionHandler
{
    private readonly IOrderSummaryProjectionRepository _repository;

    public OrderProjectionHandler(IOrderSummaryProjectionRepository repository)
    {
        _repository = repository;
    }

    public async Task ApplyAsync(
        OrderProjectionEvent orderEvent,
        CancellationToken cancellationToken = default)
    {
        Validate(orderEvent);

        await _repository.ApplyAsync(orderEvent, cancellationToken);
    }

    private static void Validate(OrderProjectionEvent orderEvent)
    {
        if (orderEvent.EventId == Guid.Empty)
        {
            throw new ArgumentException("EventId is required.");
        }

        if (!OrderProjectionEventTypes.IsSupported(orderEvent.EventType))
        {
            throw new ArgumentException($"Unsupported order event type '{orderEvent.EventType}'.");
        }

        if (orderEvent.OrderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId is required.");
        }

        if (orderEvent.CustomerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId is required.");
        }

        if (string.IsNullOrWhiteSpace(orderEvent.CustomerName))
        {
            throw new ArgumentException("CustomerName is required.");
        }

        if (orderEvent.TotalAmount < 0)
        {
            throw new ArgumentException("TotalAmount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(orderEvent.Currency))
        {
            throw new ArgumentException("Currency is required.");
        }

        if (orderEvent.OccurredAtUtc == default)
        {
            throw new ArgumentException("OccurredAtUtc is required.");
        }

        foreach (var item in orderEvent.Items)
        {
            if (item.ProductId == Guid.Empty)
            {
                throw new ArgumentException("ProductId is required.");
            }

            if (string.IsNullOrWhiteSpace(item.ProductName))
            {
                throw new ArgumentException("ProductName is required.");
            }

            if (item.Quantity <= 0)
            {
                throw new ArgumentException("Quantity must be greater than 0.");
            }

            if (item.UnitPrice < 0)
            {
                throw new ArgumentException("UnitPrice cannot be negative.");
            }
        }
    }
}
