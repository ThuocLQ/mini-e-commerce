using MediatR;
using OrderingService.Application.Orders;

namespace OrderingService.Application.Orders.CancelOrder;

public sealed record CancelOrderCommand(
    Guid OrderId,
    Guid CustomerId,
    string? Reason) : IRequest<OrderDto?>;