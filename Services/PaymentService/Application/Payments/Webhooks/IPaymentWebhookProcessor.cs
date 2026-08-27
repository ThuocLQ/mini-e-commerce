namespace PaymentService.Application.Payments.Webhooks;

public interface IPaymentWebhookProcessor
{
    Task<PaymentWebhookProcessingResult> ProcessAsync(
        string rawBody,
        string? signature,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentWebhookProcessingResult(PaymentDto? Payment, bool IsDuplicate);

public sealed record PaymentWebhookPayload(
    Guid PaymentId,
    string? ProviderEventId,
    string ProviderTransactionId,
    string Status,
    string? FailureReason);
