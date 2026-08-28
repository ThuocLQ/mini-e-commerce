using MediatR;

namespace PaymentService.Application.Payments.GetPaymentByOrderId;

public sealed record GetPaymentByOrderIdQuery(Guid OrderId) : IRequest<PaymentDto?>;