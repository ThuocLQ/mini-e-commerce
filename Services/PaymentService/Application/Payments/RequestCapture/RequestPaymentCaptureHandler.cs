using MediatR;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Payments.Providers;
using PaymentService.Application.Payments.Webhooks;

namespace PaymentService.Application.Payments.RequestCapture;

public sealed class RequestPaymentCaptureHandler
    : IRequestHandler<RequestPaymentCaptureCommand, PaymentCaptureRequestApplyResult>
{
    internal const string ConsumerName = "PaymentService.PaymentCaptureRequested";

    private readonly IPaymentUnitOfWork _unitOfWork;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentInboxRepository _inboxRepository;
    private readonly IPaymentProvider? _paymentProvider;
    private readonly IPaymentWebhookProcessor? _webhookProcessor;
    private readonly IPaymentOperationalActionRepository? _operationalActionRepository;

    public RequestPaymentCaptureHandler(
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

    public async Task<PaymentCaptureRequestApplyResult> Handle(
        RequestPaymentCaptureCommand request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var result = await _unitOfWork.ExecuteAsync(async transaction =>
        {
            var payment = await _paymentRepository.GetByIdAsync(
                request.PaymentId,
                transaction,
                cancellationToken)
                ?? throw new InvalidOperationException($"Payment '{request.PaymentId}' was not found.");

            EnsureMatchesPayment(request, payment);

            if (!await _inboxRepository.TryRecordAsync(
                    request.EventId,
                    ConsumerName,
                    transaction,
                    cancellationToken))
            {
                return new PaymentCaptureRequestApplyResult(payment.Id, true, false);
            }

            var statusBeforeRequest = payment.Status;
            payment.RequestCapture(request.RequestedAtUtc);

            var captureWasRequested = payment.Status != statusBeforeRequest;
            if (captureWasRequested && _operationalActionRepository is not null)
            {
                await _operationalActionRepository.CreateAsync(
                    Domain.Payments.PaymentOperationalAction.Create(
                        payment.Id,
                        "Capture",
                        "OrderingSaga",
                        "Capture requested by the order settlement saga.",
                        request.RequestedAtUtc),
                    transaction,
                    cancellationToken);
            }
            if (captureWasRequested && !await _paymentRepository.UpdateAsync(payment, transaction, cancellationToken))
            {
                throw new InvalidOperationException($"Payment '{payment.Id}' disappeared while requesting capture.");
            }

            return new PaymentCaptureRequestApplyResult(payment.Id, false, captureWasRequested);
        }, cancellationToken);

        if (result.CaptureWasRequested && _paymentProvider is not null && _webhookProcessor is not null)
        {
            var payment = await _paymentRepository.GetByIdAsync(result.PaymentId, cancellationToken);
            if (payment is not null)
            {
                var webhook = await _paymentProvider.RequestCaptureAsync(payment, cancellationToken);
                if (webhook is not null)
                {
                    await _webhookProcessor.ProcessAsync(webhook.RawBody, webhook.Signature, cancellationToken);
                }
            }
        }

        return result;
    }

    private static void Validate(RequestPaymentCaptureCommand request)
    {
        if (request.EventId == Guid.Empty) throw new ArgumentException("Event id cannot be empty.", nameof(request));
        if (request.PaymentId == Guid.Empty) throw new ArgumentException("Payment id cannot be empty.", nameof(request));
        if (request.OrderId == Guid.Empty) throw new ArgumentException("Order id cannot be empty.", nameof(request));
        if (request.CustomerId == Guid.Empty) throw new ArgumentException("Customer id cannot be empty.", nameof(request));
        if (request.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(request), "Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.Currency)) throw new ArgumentException("Currency is required.", nameof(request));
    }

    private static void EnsureMatchesPayment(RequestPaymentCaptureCommand request, Domain.Payments.Payment payment)
    {
        if (payment.OrderId != request.OrderId ||
            payment.CustomerId != request.CustomerId ||
            payment.Amount != request.Amount ||
            !string.Equals(payment.Currency, request.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Payment capture request '{request.EventId}' does not match payment '{payment.Id}'.");
        }
    }
}
