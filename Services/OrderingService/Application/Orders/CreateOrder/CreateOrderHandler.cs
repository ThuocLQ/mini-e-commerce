using MediatR;
using MassTransit;
using Microsoft.Extensions.Options;
using OrderingService.Application.Abstractions;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.CreateOrder;

public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderDto>
{
    private readonly IOrderRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly OrderEventOptions _eventOptions;

    public CreateOrderHandler(
        IOrderRepository repository,
        IPublishEndpoint publishEndpoint,
        IOptions<OrderEventOptions> eventOptions)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _eventOptions = eventOptions.Value;
    }

    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Order must have at least one item.");
        }

        var order = new Order(
            Guid.NewGuid(),
            request.CustomerId,
            DateTime.UtcNow,
            OrderStatus.Pending);

        foreach (var item in request.Items)
        {
            order.AddItem(new OrderItem(
                Guid.NewGuid(),
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity));
        }

        var createdOrder = await _repository.CreateAsync(order, cancellationToken);
        var orderCreatedEvent = OrderCreatedEvent.FromOrder(createdOrder, _eventOptions.Currency);

        await _publishEndpoint.Publish(orderCreatedEvent, cancellationToken);

        return OrderMapper.ToDto(createdOrder);
    }
}
