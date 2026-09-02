using MediatR;
using PaymentService.Application.Abstractions;

namespace PaymentService.Application.Payments.GetPaymentOperationalActions;

public sealed class GetPaymentOperationalActionsHandler
    : IRequestHandler<GetPaymentOperationalActionsQuery, IReadOnlyList<PaymentOperationalActionDto>>
{
    private readonly IPaymentOperationalActionRepository _repository;

    public GetPaymentOperationalActionsHandler(IPaymentOperationalActionRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<PaymentOperationalActionDto>> Handle(
        GetPaymentOperationalActionsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.PaymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment id cannot be empty.", nameof(request));
        }

        var actions = await _repository.GetByPaymentIdAsync(request.PaymentId, cancellationToken);
        return actions.Select(action => new PaymentOperationalActionDto(
            action.Id,
            action.ActionType,
            action.RequestedBy,
            action.Reason,
            action.RequestedAtUtc,
            action.CompletedAtUtc,
            action.FailureReason)).ToList();
    }
}