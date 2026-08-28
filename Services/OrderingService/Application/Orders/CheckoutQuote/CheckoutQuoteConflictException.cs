namespace OrderingService.Application.Orders.CheckoutQuote;

public sealed class CheckoutQuoteConflictException(string message, string errorCode = "CHECKOUT_QUOTE_STALE")
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
