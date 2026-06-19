using MediatR;

namespace OrderingService.Application.OrderPaymentSagas.ApplyPaymentEvent;

public sealed record ApplyPaymentSagaEventCommand(
    Guid EventId,
    OrderPaymentSagaEventType EventType,
    Guid OrderId,
    Guid PaymentId,
    string? FailureReason) : IRequest<OrderPaymentSagaDto?>;

public enum OrderPaymentSagaEventType
{
    PaymentSucceeded = 1,
    PaymentFailed = 2,
    PaymentTimedOut = 3
}
