using MediatR;
using PaymentService.Application.Abstractions;

namespace PaymentService.Application.Payments.GetPayments;

public sealed class GetPaymentsHandler(IPaymentRepository repository)
    : IRequestHandler<GetPaymentsQuery, IReadOnlyList<AdminPaymentDto>>
{
    public async Task<IReadOnlyList<AdminPaymentDto>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 200);
        var payments = await repository.GetRecentAsync(limit, cancellationToken);
        return payments.Select(PaymentMapper.ToDto).Select(AdminPaymentDto.From).ToList();
    }
}
