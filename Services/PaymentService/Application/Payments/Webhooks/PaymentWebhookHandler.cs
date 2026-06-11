using MediatR;
using PaymentService.Application.Abstractions;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Webhooks;

public sealed class PaymentWebhookHandler : IRequestHandler<PaymentWebhookCommand, PaymentDto?>
{
    private readonly IPaymentWebhookRepository _repository;

    public PaymentWebhookHandler(IPaymentWebhookRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaymentDto?> Handle(PaymentWebhookCommand request, CancellationToken cancellationToken)
    {
        var normalizedStatus = request.Status.Trim().ToUpperInvariant();
        var status = normalizedStatus switch
        {
            "SUCCEEDED" => PaymentStatus.Succeeded,
            "FAILED" => PaymentStatus.Failed,
            _ => throw new InvalidOperationException($"Unsupported payment webhook status '{request.Status}'.")
        };

        var providerEventId = string.IsNullOrWhiteSpace(request.ProviderEventId)
            ? $"{request.PaymentId:N}:{request.ProviderTransactionId.Trim()}:{normalizedStatus}"
            : request.ProviderEventId.Trim();

        var payment = await _repository.ApplyAsync(
            providerEventId,
            request.PaymentId,
            request.ProviderTransactionId,
            status,
            request.FailureReason,
            DateTime.UtcNow,
            cancellationToken);

        return payment is null ? null : PaymentMapper.ToDto(payment);
    }
}
