namespace OrderingService.Application.Orders;

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    DateTime CreatedAtUtc,
    string Status,
    decimal TotalAmount,
    string Currency,
    decimal SubtotalAmount,
    string? DiscountCode,
    decimal DiscountAmount,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice);
