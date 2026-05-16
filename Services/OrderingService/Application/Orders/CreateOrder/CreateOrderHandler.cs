using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.CreateOrder;

public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderDto>
{
    private readonly IOrderRepository _repository;

    public CreateOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
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

        return OrderMapper.ToDto(createdOrder);
    }
}
