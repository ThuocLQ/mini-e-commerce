using MediatR;
using PaymentService.Application.Abstractions;

namespace PaymentService.Application.Payments.Webhooks;

public sealed class PaymentWebhookHandler : IRequestHandler<PaymentWebhookCommand, PaymentDto?>
{
    private readonly IPaymentRepository _repository;

    public PaymentWebhookHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaymentDto?> Handle(PaymentWebhookCommand request, CancellationToken cancellationToken)
    {
        var payment = await _repository.GetByIdAsync(request.PaymentId, cancellationToken);

        if (payment is null)
        {
            return null;
        }

        var normalizedStatus = request.Status.Trim().ToUpperInvariant();

        if (normalizedStatus == "SUCCEEDED")
        {
            payment.MarkSucceeded(request.ProviderTransactionId, DateTime.UtcNow);
        }
        else if (normalizedStatus == "FAILED")
        {
            payment.MarkFailed(request.FailureReason ?? "Payment failed by provider.", DateTime.UtcNow);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported payment webhook status '{request.Status}'.");
        }

        await _repository.UpdateAsync(payment, cancellationToken);

        return PaymentMapper.ToDto(payment);
    }
}