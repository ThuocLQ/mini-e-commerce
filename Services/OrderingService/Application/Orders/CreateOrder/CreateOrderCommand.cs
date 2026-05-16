using MediatR;

namespace OrderingService.Application.Orders.CreateOrder;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    IReadOnlyList<CreateOrderItemCommand> Items) : IRequest<OrderDto>;

public sealed record CreateOrderItemCommand(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity);
