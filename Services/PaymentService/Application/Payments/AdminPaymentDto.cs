namespace PaymentService.Application.Payments;

// This is deliberately separate from PaymentDto, which may contain a customer checkout URL.
public sealed record AdminPaymentDto(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string Status,
    string? ProviderTransactionId,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? Provider)
{
    public static AdminPaymentDto From(PaymentDto payment) => new(
        payment.Id,
        payment.OrderId,
        payment.CustomerId,
        payment.Amount,
        payment.Currency,
        payment.Status,
        payment.ProviderTransactionId,
        payment.FailureReason,
        payment.CreatedAtUtc,
        payment.CompletedAtUtc,
        payment.Provider);
}