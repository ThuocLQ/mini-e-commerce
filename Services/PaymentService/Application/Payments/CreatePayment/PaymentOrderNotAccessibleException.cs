namespace PaymentService.Application.Payments.CreatePayment;

public sealed class PaymentOrderNotAccessibleException : KeyNotFoundException
{
    public PaymentOrderNotAccessibleException(Guid orderId)
        : base($"Order {orderId:D} was not found.")
    {
    }
}
