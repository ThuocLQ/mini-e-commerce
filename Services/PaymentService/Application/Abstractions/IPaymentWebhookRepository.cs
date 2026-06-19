using PaymentService.Domain.Payments;
using PaymentService.Application.Payments.Webhooks;

namespace PaymentService.Application.Abstractions;

public interface IPaymentWebhookRepository
{
    Task<PaymentWebhookApplyResult> ApplyAsync(
        string providerEventId,
        Guid paymentId,
        string providerTransactionId,
        PaymentStatus status,
        string? failureReason,
        string payloadHash,
        string signatureStatus,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default);

    Task RecordRejectedAsync(
        string providerEventId,
        Guid paymentId,
        string providerTransactionId,
        string eventType,
        string payloadHash,
        string signatureStatus,
        string error,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default);
}
