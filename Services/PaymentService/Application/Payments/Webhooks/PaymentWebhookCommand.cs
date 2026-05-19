using MediatR;

namespace PaymentService.Application.Payments.Webhooks;

public sealed record PaymentWebhookCommand(
    Guid PaymentId,
    string ProviderTransactionId,
    string Status,
    string? FailureReason) : IRequest<PaymentDto?>;