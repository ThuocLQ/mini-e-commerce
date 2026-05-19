namespace PaymentService.API.Contracts;

public sealed record CreatePaymentRequest(
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency);
