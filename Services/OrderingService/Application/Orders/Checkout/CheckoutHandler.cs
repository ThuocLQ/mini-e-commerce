using MediatR;
using MassTransit;
using Microsoft.Extensions.Options;
using OrderingService.Application.Abstractions;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.Checkout;

public class CheckoutHandler : IRequestHandler<CheckoutCommand, OrderDto>
{
    private readonly IBasketClient _basketClient;
    private readonly IOrderRepository _orderRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly OrderEventOptions _eventOptions;

    public CheckoutHandler(
        IBasketClient basketClient,
        IOrderRepository orderRepository,
        IPublishEndpoint publishEndpoint,
        IOptions<OrderEventOptions> eventOptions)
    {
        _basketClient = basketClient;
        _orderRepository = orderRepository;
        _publishEndpoint = publishEndpoint;
        _eventOptions = eventOptions.Value;
    }
    
    public async Task<OrderDto> Handle(CheckoutCommand request, CancellationToken cancellationToken)
    {
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        var existingOrder = await _orderRepository.GetByCustomerAndIdempotencyKeyAsync(
            request.CustomerId,
            idempotencyKey,
            cancellationToken);

        if (existingOrder is not null)
        {
            await _basketClient.ClearBasketAsync(request.CustomerId, cancellationToken);
            return OrderMapper.ToDto(existingOrder);
        }

        var basket = await _basketClient.GetBasketAsync(request.CustomerId, cancellationToken);

        if (basket is null || basket.Items is null || basket.Items.Count == 0)
        {
            throw new InvalidOperationException("Basket is empty.");
        }

        if (!string.Equals(basket.UserId, request.CustomerId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Basket does not belong to the requested customer.");
        }

        var order = new Order(
            Guid.NewGuid(),
            request.CustomerId,
            DateTime.UtcNow,
            OrderStatus.Pending,
            idempotencyKey);

        foreach (var item in basket.Items)
        {
            if (!Guid.TryParse(item.ProductId, out var productId) || productId == Guid.Empty)
            {
                throw new ArgumentException("Basket contains an invalid product id.");
            }

            if (string.IsNullOrWhiteSpace(item.ProductName))
            {
                throw new ArgumentException("Basket contains an item without product name.");
            }

            order.AddItem(new OrderItem(
                Guid.NewGuid(),
                productId,
                item.ProductName,
                item.Price,
                item.Quantity));
        }

        var createdOrder = await _orderRepository.CreateAsync(order, cancellationToken);
        var orderCreatedEvent = OrderIntegrationEventFactory.CreateOrderCreated(createdOrder, _eventOptions.Currency);

        await _publishEndpoint.Publish(orderCreatedEvent, cancellationToken);
        await _basketClient.ClearBasketAsync(request.CustomerId, cancellationToken);

        return OrderMapper.ToDto(createdOrder);
    }

    private static string NormalizeIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency-Key header or idempotencyKey body field is required.");
        }

        idempotencyKey = idempotencyKey.Trim();
        if (idempotencyKey.Length > 128)
        {
            throw new ArgumentException("Idempotency key cannot exceed 128 characters.");
        }

        return idempotencyKey;
    }
}
