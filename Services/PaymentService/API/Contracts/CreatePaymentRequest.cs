namespace PaymentService.API.Contracts;

public sealed record CreatePaymentRequest(
    Guid OrderId);
