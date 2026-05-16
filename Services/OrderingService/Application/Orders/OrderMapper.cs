using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders;

public static class OrderMapper
{
    public static OrderDto ToDto(Order order)
    {
        return new OrderDto(
            order.Id,
            order.CustomerId,
            order.CreatedAtUtc,
            order.Status.ToString(),
            order.TotalAmount,
            order.Items.Select(item => new OrderItemDto(
                item.Id,
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity,
                item.TotalPrice)).ToList());
    }
}