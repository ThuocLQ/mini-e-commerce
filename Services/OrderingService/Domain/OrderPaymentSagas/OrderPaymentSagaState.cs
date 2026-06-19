namespace OrderingService.Domain.OrderPaymentSagas;

public enum OrderPaymentSagaState
{
    PaymentRequested = 1,
    OrderPaid = 2,
    OrderCancelled = 3,
    TimedOut = 4,
    CompensationRequired = 5
}
