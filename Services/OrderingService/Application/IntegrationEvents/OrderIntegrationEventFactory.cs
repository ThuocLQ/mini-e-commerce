using BuildingBlocks.Contracts.Events.Orders;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.IntegrationEvents;

public static class OrderIntegrationEventFactory
{
    public static OrderCreatedIntegrationEvent CreateOrderCreated(Order order, string currency)
    {
        return new OrderCreatedIntegrationEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            Currency = currency
        };
    }
}
