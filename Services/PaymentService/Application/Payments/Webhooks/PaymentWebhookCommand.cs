using MediatR;

namespace PaymentService.Application.Payments.Webhooks;

public sealed record PaymentWebhookCommand(
    Guid PaymentId,
    string? ProviderEventId,
    string ProviderTransactionId,
    string Status,
    string? FailureReason,
    string PayloadHash,
    string SignatureStatus) : IRequest<PaymentDto?>;
