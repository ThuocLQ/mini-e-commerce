namespace OrderingService.API.Contracts;

public sealed record CheckoutQuoteRequest(
    Guid BasketId,
    long BasketVersion,
    string? CouponCode,
    Guid? ShippingAddressId);
