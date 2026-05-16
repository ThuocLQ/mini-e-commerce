using MediatR;

namespace OrderingService.Application.Orders.Checkout;

public sealed record CheckoutCommand(Guid CustomerId, string? IdempotencyKey) : IRequest<OrderDto>;
