namespace OrderingService.Application.Orders.Checkout;

public sealed class CheckoutIdempotencyConflictException : Exception
{
    public CheckoutIdempotencyConflictException(string message)
        : base(message)
    {
    }
}
