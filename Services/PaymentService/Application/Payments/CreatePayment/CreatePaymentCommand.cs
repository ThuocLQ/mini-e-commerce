using MediatR;

namespace PaymentService.Application.Payments.CreatePayment;

public sealed record CreatePaymentCommand(
    Guid OrderId) : IRequest<PaymentDto>;
