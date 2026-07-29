namespace PaymentService.Application.Abstractions;

public interface IOrderPaymentClient
{
    Task<OrderPaymentSnapshot?> GetOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}

public sealed record OrderPaymentSnapshot(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    string Currency,
    string Status);
