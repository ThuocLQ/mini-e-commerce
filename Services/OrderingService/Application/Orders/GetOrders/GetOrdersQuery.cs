using MediatR;

namespace OrderingService.Application.Orders.GetOrders;

public sealed record GetOrdersQuery(Guid CustomerId) : IRequest<IReadOnlyList<OrderDto>>;
