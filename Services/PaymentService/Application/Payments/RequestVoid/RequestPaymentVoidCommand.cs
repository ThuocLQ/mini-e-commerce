using MediatR;

namespace PaymentService.Application.Payments.RequestVoid;

public sealed record RequestPaymentVoidCommand(
    Guid EventId,
    Guid PaymentId,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string Reason,
    DateTime RequestedAtUtc) : IRequest<PaymentVoidRequestApplyResult>;

public sealed record PaymentVoidRequestApplyResult(
    Guid PaymentId,
    bool WasAlreadyProcessed,
    bool VoidWasRequested);
