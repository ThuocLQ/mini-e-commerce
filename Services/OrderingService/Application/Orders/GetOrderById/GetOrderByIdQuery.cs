using MediatR;

namespace OrderingService.Application.Orders.GetOrderById;

public sealed record GetOrderByIdQuery(Guid Id) : IRequest<OrderDto?>;
