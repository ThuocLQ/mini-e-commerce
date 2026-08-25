using MediatR;

namespace PaymentService.Application.Payments.RequestCapture;

public sealed record RequestPaymentCaptureCommand(
    Guid EventId,
    Guid PaymentId,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    DateTime RequestedAtUtc) : IRequest<PaymentCaptureRequestApplyResult>;

public sealed record PaymentCaptureRequestApplyResult(
    Guid PaymentId,
    bool WasAlreadyProcessed,
    bool CaptureWasRequested);
