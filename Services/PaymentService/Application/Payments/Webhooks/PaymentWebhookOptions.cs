namespace PaymentService.Application.Payments.Webhooks;

public sealed class PaymentWebhookOptions
{
    public const string SectionName = "PaymentWebhooks";

    public string SignatureHeaderName { get; init; } = "X-MicroShop-Signature";
    public string SharedSecret { get; init; } = "dev-webhook-secret";
    public bool RequireSignature { get; init; } = true;
}
