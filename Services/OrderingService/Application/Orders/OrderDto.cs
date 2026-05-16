namespace OrderingService.Application.Orders;

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    DateTime CreatedAtUtc,
    string Status,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice);