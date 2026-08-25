using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Contracts.Events.Orders;
using OrderingService.Domain.OrderPaymentSagas;
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
        return CreateOrderProjectionStatusChanged(order, OrderStatus.PendingPayment);
    }

    public static OrderStatusChangedIntegrationEvent CreateOrderStatusChanged(Order order, OrderStatus previousStatus)
    {
        return new OrderStatusChangedIntegrationEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            PreviousStatus = previousStatus.ToString(),
            CurrentStatus = order.Status.ToString(),
            TotalAmount = order.TotalAmount,
            Currency = order.Currency
        };
    }

    public static OrderPaymentSagaStateChangedIntegrationEvent CreatePaymentSagaStateChanged(
        OrderPaymentSaga saga,
        OrderPaymentSagaState previousState,
        string? causationId = null)
    {
        return new OrderPaymentSagaStateChangedIntegrationEvent
        {
            OrderId = saga.OrderId,
            PaymentId = saga.PaymentId,
            PreviousState = previousState.ToString(),
            CurrentState = saga.State.ToString(),
            Reason = saga.LastError,
            CausationId = causationId
        };
    }

    public static MicroShopEventEnvelope<OrderProjectionEventData> CreateOrderProjectionStatusChanged(
        Order order,
        OrderStatus previousStatus)
    {
        var occurredAtUtc = DateTime.UtcNow;

        return new MicroShopEventEnvelope<OrderProjectionEventData>
        {
            EventType = GetProjectionEventType(order.Status),
            EventVersion = 1,
            Source = "OrderingService",
            Subject = $"orders/{order.Id:D}",
            OccurredAtUtc = occurredAtUtc,
            Data = new OrderProjectionEventData
            {
                Sequence = GetProjectionSequence(order.Status),
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

    private static string GetProjectionEventType(OrderStatus status) => status switch
    {
        OrderStatus.Pending or OrderStatus.PendingPayment => "OrderCreated",
        OrderStatus.Paid => "OrderPaid",
        OrderStatus.Refunded => "OrderRefunded",
        OrderStatus.PaymentFailed => "OrderPaymentFailed",
        OrderStatus.Cancelled => "OrderCancelled",
        _ => throw new InvalidOperationException($"Order status '{status}' has no projection event type.")
    };

    private static long GetProjectionSequence(OrderStatus status) => status switch
    {
        OrderStatus.Pending or OrderStatus.PendingPayment => 1,
        OrderStatus.Paid or OrderStatus.PaymentFailed => 2,
        OrderStatus.Cancelled => 3,
        OrderStatus.Refunded => 4,
        _ => throw new InvalidOperationException($"Order status '{status}' has no projection sequence.")
    };
}
