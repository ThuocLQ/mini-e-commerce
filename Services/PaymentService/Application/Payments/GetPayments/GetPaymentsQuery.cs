using MediatR;
using PaymentService.Application.Payments;

namespace PaymentService.Application.Payments.GetPayments;

public sealed record GetPaymentsQuery(int Limit = 100) : IRequest<IReadOnlyList<AdminPaymentDto>>;
