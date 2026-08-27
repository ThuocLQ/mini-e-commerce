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
            order.Currency,
            order.SubtotalAmount,
            order.DiscountCode,
            order.DiscountAmount,
            order.Items.Select(item => new OrderItemDto(
                item.Id,
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity,
                item.TotalPrice)).ToList(),
            order.ShippingAddress is null ? null : new OrderAddressSnapshotDto(
                order.ShippingAddress.AddressId,
                order.ShippingAddress.Label,
                order.ShippingAddress.RecipientName,
                order.ShippingAddress.Line1,
                order.ShippingAddress.Line2,
                order.ShippingAddress.City,
                order.ShippingAddress.CountryCode,
                order.ShippingAddress.PostalCode));
    }
}
