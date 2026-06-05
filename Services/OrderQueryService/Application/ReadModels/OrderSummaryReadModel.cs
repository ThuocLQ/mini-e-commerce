namespace OrderQueryService.Application.ReadModels;

public sealed class OrderSummaryReadModel
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = default!;
    public string Status { get; init; } = default!;
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = default!;
    public int ItemCount { get; init; }
    public IReadOnlyList<OrderSummaryItemReadModel> Items { get; init; } = [];
    public DateTime CreatedAtUtc { get; init; }
    public DateTime LastUpdatedAtUtc { get; init; }
    public DateTime? PaidAtUtc { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public Guid? LastProjectedEventId { get; init; }
    public string? LastProjectedEventType { get; init; }
    public DateTime? LastProjectedEventOccurredAtUtc { get; init; }
    public DateTime? LastProjectedAtUtc { get; init; }
}

public sealed class OrderSummaryItemReadModel
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
