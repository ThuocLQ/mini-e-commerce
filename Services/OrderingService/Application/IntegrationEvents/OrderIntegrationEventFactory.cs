using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Contracts.Events.Orders;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.IntegrationEvents;

public static class OrderIntegrationEventFactory
{
    public static OrderCreatedIntegrationEvent CreateOrderCreated(Order order)
    {
        return new OrderCreatedIntegrationEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency
        };
    }

    public static MicroShopEventEnvelope<OrderProjectionEventData> CreateOrderProjectionCreated(Order order)
    {
        var occurredAtUtc = DateTime.UtcNow;

        return new MicroShopEventEnvelope<OrderProjectionEventData>
        {
            EventType = "OrderCreated",
            EventVersion = 1,
            Source = "OrderingService",
            Subject = $"orders/{order.Id:D}",
            OccurredAtUtc = occurredAtUtc,
            Data = new OrderProjectionEventData
            {
                // Order creation is version one; later order state transitions must advance it.
                Sequence = 1,
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                // Identity profile is not part of the checkout aggregate yet; do not synchronously call Identity here.
                CustomerName = order.CustomerId.ToString("D"),
                TotalAmount = order.TotalAmount,
                Currency = order.Currency,
                ItemCount = order.Items.Count,
                Items = order.Items.Select(item => new OrderProjectionItemData
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList()
            }
        };
    }
}
