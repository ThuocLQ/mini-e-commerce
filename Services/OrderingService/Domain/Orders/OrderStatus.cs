namespace OrderingService.Domain.Orders;

public enum OrderStatus
{
    Pending = 1,
    PendingPayment = 2,
    Paid = 3,
    PaymentFailed = 4,
    Confirmed = 5,
    Shipped = 6,
    Delivered = 7,
    Cancelled = 8
}
