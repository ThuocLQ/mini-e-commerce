using OrderingService.Domain.OrderPaymentSagas;

namespace OrderingService.Application.OrderPaymentSagas;

public static class OrderPaymentSagaMapper
{
    public static OrderPaymentSagaDto ToDto(OrderPaymentSaga saga)
    {
        return new OrderPaymentSagaDto(
            saga.Id,
            saga.OrderId,
            saga.PaymentId,
            saga.State.ToString(),
            saga.StartedAtUtc,
            saga.UpdatedAtUtc,
            saga.TimeoutAtUtc,
            saga.LastProcessedEventId,
            saga.LastError);
    }
}
