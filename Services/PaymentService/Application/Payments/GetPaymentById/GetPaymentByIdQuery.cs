using MediatR;

namespace PaymentService.Application.Payments.GetPaymentById;

public sealed record GetPaymentByIdQuery(Guid Id) : IRequest<PaymentDto?>;