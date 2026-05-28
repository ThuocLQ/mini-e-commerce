namespace OrderQueryService.API.Contracts;

public sealed record DebugUpsertOrderSummaryRequest(
    Guid OrderId,
    Guid CustomerId,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    string Currency,
    int ItemCount,
    IReadOnlyList<DebugUpsertOrderSummaryItemRequest>? Items);

public sealed record DebugUpsertOrderSummaryItemRequest(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);
