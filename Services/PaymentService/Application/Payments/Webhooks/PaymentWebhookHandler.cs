using MediatR;
using PaymentService.Application.Abstractions;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Webhooks;

public sealed class PaymentWebhookHandler : IRequestHandler<PaymentWebhookCommand, PaymentDto?>
{
    private readonly IPaymentWebhookRepository _repository;
    private readonly IPaymentMetrics _metrics;

    public PaymentWebhookHandler(
        IPaymentWebhookRepository repository,
        IPaymentMetrics metrics)
    {
        _repository = repository;
        _metrics = metrics;
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

        if (!string.Equals(request.SignatureStatus, "Verified", StringComparison.OrdinalIgnoreCase))
        {
            await _repository.RecordRejectedAsync(
                providerEventId,
                request.PaymentId,
                request.ProviderTransactionId,
                normalizedStatus,
                request.PayloadHash,
                request.SignatureStatus,
                "Webhook signature verification failed.",
                DateTime.UtcNow,
                cancellationToken);

            _metrics.RecordWebhookRequest("rejected");
            throw new UnauthorizedAccessException("Webhook signature verification failed.");
        }

        var result = await _repository.ApplyAsync(
            providerEventId,
            request.PaymentId,
            request.ProviderTransactionId,
            status,
            request.FailureReason,
            request.PayloadHash,
            request.SignatureStatus,
            DateTime.UtcNow,
            cancellationToken);

        _metrics.RecordWebhookRequest(
            result.IsDuplicate
                ? "duplicate"
                : result.Payment is null
                    ? "not_found"
                    : "accepted");

        return result.Payment is null ? null : PaymentMapper.ToDto(result.Payment);
    }
}
