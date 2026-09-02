using MediatR;

namespace PaymentService.Application.Payments.CreatePayment;

public sealed record CreatePaymentCommand(
    Guid OrderId,
    Guid CustomerId,
    string IdempotencyKey,
    string? Provider) : IRequest<CreatePaymentResult>;