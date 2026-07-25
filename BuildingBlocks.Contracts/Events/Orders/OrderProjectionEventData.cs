namespace BuildingBlocks.Contracts.Events.Orders;

// Contract consumed by Kafka read-model projections. Keep it additive and versioned by its envelope.
public sealed record OrderProjectionEventData
{
    public long Sequence { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "VND";
    public int ItemCount { get; init; }
    public IReadOnlyList<OrderProjectionItemData> Items { get; init; } = [];
}

public sealed record OrderProjectionItemData
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
