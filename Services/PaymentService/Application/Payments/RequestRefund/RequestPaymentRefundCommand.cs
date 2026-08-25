using MediatR;

namespace PaymentService.Application.Payments.RequestRefund;

public sealed record RequestPaymentRefundCommand(
    Guid EventId,
    Guid PaymentId,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string Reason,
    DateTime RequestedAtUtc) : IRequest<PaymentRefundRequestApplyResult>;

public sealed record PaymentRefundRequestApplyResult(
    Guid PaymentId,
    bool WasAlreadyProcessed,
    bool RefundWasRequested);
