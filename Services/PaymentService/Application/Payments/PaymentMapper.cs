using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments;

public static class PaymentMapper
{
    public static PaymentDto ToDto(Payment payment)
    {
        return new PaymentDto(
            payment.Id,
            payment.OrderId,
            payment.CustomerId,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            payment.ProviderTransactionId,
            payment.FailureReason,
            payment.CreatedAtUtc,
            payment.CompletedAtUtc);
    }
}