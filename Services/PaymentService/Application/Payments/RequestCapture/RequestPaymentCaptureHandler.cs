using MediatR;
using PaymentService.Application.Abstractions;

namespace PaymentService.Application.Payments.RequestCapture;

public sealed class RequestPaymentCaptureHandler
    : IRequestHandler<RequestPaymentCaptureCommand, PaymentCaptureRequestApplyResult>
{
    internal const string ConsumerName = "PaymentService.PaymentCaptureRequested";

    private readonly IPaymentUnitOfWork _unitOfWork;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentInboxRepository _inboxRepository;

    public RequestPaymentCaptureHandler(
        IPaymentUnitOfWork unitOfWork,
        IPaymentRepository paymentRepository,
        IPaymentInboxRepository inboxRepository)
    {
        _unitOfWork = unitOfWork;
        _paymentRepository = paymentRepository;
        _inboxRepository = inboxRepository;
    }

    public Task<PaymentCaptureRequestApplyResult> Handle(
        RequestPaymentCaptureCommand request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        return _unitOfWork.ExecuteAsync(async transaction =>
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
            if (captureWasRequested && !await _paymentRepository.UpdateAsync(payment, transaction, cancellationToken))
            {
                throw new InvalidOperationException($"Payment '{payment.Id}' disappeared while requesting capture.");
            }

            return new PaymentCaptureRequestApplyResult(payment.Id, false, captureWasRequested);
        }, cancellationToken);
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
