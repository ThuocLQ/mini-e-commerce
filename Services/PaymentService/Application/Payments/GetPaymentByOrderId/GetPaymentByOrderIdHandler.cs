using MediatR;
using PaymentService.Application.Abstractions;

namespace PaymentService.Application.Payments.GetPaymentByOrderId;

public sealed class GetPaymentByOrderIdHandler(IPaymentRepository repository)
    : IRequestHandler<GetPaymentByOrderIdQuery, PaymentDto?>
{
    public async Task<PaymentDto?> Handle(GetPaymentByOrderIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await repository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        return payment is null ? null : PaymentMapper.ToDto(payment);
    }
}