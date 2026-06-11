using PaymentService.Domain.Payments;

namespace PaymentService.Application.Abstractions;

public interface IPaymentWebhookRepository
{
    Task<Payment?> ApplyAsync(
        string providerEventId,
        Guid paymentId,
        string providerTransactionId,
        PaymentStatus status,
        string? failureReason,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default);
}
