namespace ProjectionWorker.Application.Events;

public sealed class OrderProjectionEvent
{
    public Guid EventId { get; init; }
    public string EventType { get; init; } = default!;
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = default!;
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "VND";
    public int ItemCount { get; init; }
    public IReadOnlyList<OrderProjectionItem> Items { get; init; } = [];
    public DateTime OccurredAtUtc { get; init; }
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
}

public sealed class OrderProjectionItem
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
