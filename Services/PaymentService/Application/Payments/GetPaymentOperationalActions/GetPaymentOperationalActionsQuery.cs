using MediatR;

namespace PaymentService.Application.Payments.GetPaymentOperationalActions;

public sealed record GetPaymentOperationalActionsQuery(Guid PaymentId)
    : IRequest<IReadOnlyList<PaymentOperationalActionDto>>;