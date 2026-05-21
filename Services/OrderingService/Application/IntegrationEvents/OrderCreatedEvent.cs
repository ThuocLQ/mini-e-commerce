using OrderingService.Domain.Orders;

namespace OrderingService.Application.IntegrationEvents;

public sealed record OrderCreatedEvent
{
    public Guid EventId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "USD";

    public static OrderCreatedEvent FromOrder(Order order, string currency)
    {
        return new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            Currency = currency
        };
    }
}
