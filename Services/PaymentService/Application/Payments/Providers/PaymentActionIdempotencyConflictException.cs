namespace PaymentService.Application.Payments.Providers;

public sealed class PaymentActionIdempotencyConflictException : InvalidOperationException
{
    public PaymentActionIdempotencyConflictException(string message) : base(message)
    {
    }
}
