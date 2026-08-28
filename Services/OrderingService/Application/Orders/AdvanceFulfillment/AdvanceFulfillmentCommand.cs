using MediatR;
using OrderingService.Application.Orders;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.AdvanceFulfillment;

public sealed record AdvanceFulfillmentCommand(
    Guid OrderId,
    OrderStatus TargetStatus) : IRequest<OrderDto?>;