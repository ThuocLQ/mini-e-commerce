namespace OrderingService.Application.Orders.CheckoutQuote;

public interface ICheckoutQuoteTokenService
{
    string Create(CheckoutQuoteTokenPayload payload);

    CheckoutQuoteTokenPayload ReadAndValidate(
        string token,
        CheckoutQuoteRequestBinding request);
}

public sealed record CheckoutQuoteRequestBinding(
    Guid CustomerId,
    Guid BasketId,
    long BasketVersion,
    string? CouponCode,
    Guid? ShippingAddressId);

public sealed record CheckoutQuoteTokenPayload(
    int Version,
    Guid CustomerId,
    Guid BasketId,
    long BasketVersion,
    string? CouponCode,
    Guid? ShippingAddressId,
    IReadOnlyList<CheckoutQuoteTokenItem> Items,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    string Currency,
    DateTime EvaluatedAtUtc,
    DateTime ExpiresAtUtc);

public sealed record CheckoutQuoteTokenItem(
    Guid ProductId,
    string ProductName,
    decimal CurrentUnitPrice,
    int Quantity);
