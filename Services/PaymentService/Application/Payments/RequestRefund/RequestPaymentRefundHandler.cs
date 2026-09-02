using MediatR;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Payments.Providers;
using PaymentService.Application.Payments.Webhooks;

namespace PaymentService.Application.Payments.RequestRefund;

public sealed class RequestPaymentRefundHandler
    : IRequestHandler<RequestPaymentRefundCommand, PaymentRefundRequestApplyResult>
{
    internal const string ConsumerName = "PaymentService.PaymentRefundRequested";

    private readonly IPaymentUnitOfWork _unitOfWork;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentInboxRepository _inboxRepository;
    private readonly IPaymentProvider? _paymentProvider;
    private readonly IPaymentWebhookProcessor? _webhookProcessor;
    private readonly IPaymentOperationalActionRepository? _operationalActionRepository;

    public RequestPaymentRefundHandler(
        IPaymentUnitOfWork unitOfWork,
        IPaymentRepository paymentRepository,
        IPaymentInboxRepository inboxRepository,
        IPaymentProvider? paymentProvider = null,
        IPaymentWebhookProcessor? webhookProcessor = null,
        IPaymentOperationalActionRepository? operationalActionRepository = null)
    {
        _unitOfWork = unitOfWork;
        _paymentRepository = paymentRepository;
        _inboxRepository = inboxRepository;
        _paymentProvider = paymentProvider;
        _webhookProcessor = webhookProcessor;
        _operationalActionRepository = operationalActionRepository;
    }

    public async Task<PaymentRefundRequestApplyResult> Handle(RequestPaymentRefundCommand request, CancellationToken cancellationToken)
    {
        Validate(request);

        var result = await _unitOfWork.ExecuteAsync(async transaction =>
        {
            var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, transaction, cancellationToken)
                ?? throw new InvalidOperationException($"Payment '{request.PaymentId}' was not found.");

            EnsureMatchesPayment(request, payment);

            if (!await _inboxRepository.TryRecordAsync(request.EventId, ConsumerName, transaction, cancellationToken))
            {
                return new PaymentRefundRequestApplyResult(payment.Id, true, false);
            }

            var statusBeforeRequest = payment.Status;
            payment.RequestRefund(request.RequestedAtUtc);
            var refundWasRequested = payment.Status != statusBeforeRequest;
            if (refundWasRequested && _operationalActionRepository is not null)
            {
                await _operationalActionRepository.CreateAsync(
                    Domain.Payments.PaymentOperationalAction.Create(
                        payment.Id,
                        "Refund",
                        "OrderingSaga",
                        request.Reason,
                        request.RequestedAtUtc),
                    transaction,
                    cancellationToken);
            }
            if (refundWasRequested && !await _paymentRepository.UpdateAsync(payment, transaction, cancellationToken))
            {
                throw new InvalidOperationException($"Payment '{payment.Id}' disappeared while requesting refund.");
            }

            return new PaymentRefundRequestApplyResult(payment.Id, false, refundWasRequested);
        }, cancellationToken);

        if (result.RefundWasRequested && _paymentProvider is not null && _webhookProcessor is not null)
        {
            var payment = await _paymentRepository.GetByIdAsync(result.PaymentId, cancellationToken);
            if (payment is not null)
            {
                var webhook = await _paymentProvider.RequestRefundAsync(payment, cancellationToken);
                if (webhook is not null)
                {
                    await _webhookProcessor.ProcessAsync(webhook.RawBody, webhook.Signature, cancellationToken);
                }
            }
        }

        return result;
    }

    private static void Validate(RequestPaymentRefundCommand request)
    {
        if (request.EventId == Guid.Empty || request.PaymentId == Guid.Empty || request.OrderId == Guid.Empty || request.CustomerId == Guid.Empty)
            throw new ArgumentException("Payment refund request identifiers are required.", nameof(request));
        if (request.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(request), "Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.Currency)) throw new ArgumentException("Currency is required.", nameof(request));
    }

    private static void EnsureMatchesPayment(RequestPaymentRefundCommand request, Domain.Payments.Payment payment)
    {
        if (payment.OrderId != request.OrderId || payment.CustomerId != request.CustomerId || payment.Amount != request.Amount ||
            !string.Equals(payment.Currency, request.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Payment refund request '{request.EventId}' does not match payment '{payment.Id}'.");
    }
}
