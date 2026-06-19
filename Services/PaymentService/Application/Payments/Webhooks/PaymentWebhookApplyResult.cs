using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Webhooks;

public sealed record PaymentWebhookApplyResult(
    Payment? Payment,
    bool IsDuplicate,
    string ProviderEventId,
    PaymentStatus Status);
