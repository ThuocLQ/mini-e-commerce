using MediatR;

namespace OrderingService.Application.Orders.GetAllOrders;

public sealed record GetAllOrdersQuery : IRequest<IReadOnlyList<OrderDto>>;