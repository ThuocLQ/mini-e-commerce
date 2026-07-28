namespace PaymentService.Application.Payments.Webhooks;

public sealed class PaymentWebhookIntegrityException : InvalidOperationException
{
    public PaymentWebhookIntegrityException(string providerEventId)
        : base($"Provider event '{providerEventId}' was received with conflicting payment data.")
    {
    }
}
