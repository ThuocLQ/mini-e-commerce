using MediatR;

namespace OrderingService.Application.Orders.GetOrders;

public sealed record GetOrdersQuery : IRequest<IReadOnlyList<OrderDto>>;
