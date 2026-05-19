using MediatR;
using PaymentService.Application.Abstractions;

namespace PaymentService.Application.Payments.GetPaymentById;

public sealed class GetPaymentByIdHandler : IRequestHandler<GetPaymentByIdQuery, PaymentDto?>
{
    private readonly IPaymentRepository _repository;

    public GetPaymentByIdHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaymentDto?> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await _repository.GetByIdAsync(request.Id, cancellationToken);

        return payment is null
            ? null
            : PaymentMapper.ToDto(payment);
    }
}