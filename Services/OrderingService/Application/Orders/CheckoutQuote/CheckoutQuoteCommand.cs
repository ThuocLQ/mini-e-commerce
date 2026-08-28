using MediatR;

namespace OrderingService.Application.Orders.CheckoutQuote;

public sealed record CheckoutQuoteCommand(
    Guid CustomerId,
    Guid BasketId,
    long BasketVersion,
    string? CouponCode,
    Guid? ShippingAddressId) : IRequest<CheckoutQuoteDto>;
