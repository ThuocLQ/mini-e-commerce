namespace PaymentService.API.Contracts;

public sealed record PaymentWebhookRequest(
    Guid PaymentId,
    string ProviderTransactionId,
    string Status,
    string? FailureReason);
