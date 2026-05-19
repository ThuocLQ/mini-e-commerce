using MediatR;
using PaymentService.Application.Abstractions;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.CreatePayment;

public sealed class CreatePaymentHandler : IRequestHandler<CreatePaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _repository;

    public CreatePaymentHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaymentDto> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = new Payment(
            Guid.NewGuid(),
            request.OrderId,
            request.CustomerId,
            request.Amount,
            request.Currency,
            PaymentStatus.Pending,
            DateTime.UtcNow);

        var createdPayment = await _repository.CreateAsync(payment, cancellationToken);

        return PaymentMapper.ToDto(createdPayment);
    }
}