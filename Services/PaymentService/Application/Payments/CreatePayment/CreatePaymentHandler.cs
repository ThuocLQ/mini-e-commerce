using MediatR;
using PaymentService.Application.Abstractions;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.CreatePayment;

public sealed class CreatePaymentHandler : IRequestHandler<CreatePaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _repository;
    private readonly IOrderPaymentClient _orderClient;

    public CreatePaymentHandler(
        IPaymentRepository repository,
        IOrderPaymentClient orderClient)
    {
        _repository = repository;
        _orderClient = orderClient;
    }

    public async Task<PaymentDto> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        if (request.OrderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId is required.", nameof(request.OrderId));
        }

        var order = await _orderClient.GetOrderAsync(request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        var existingPayment = await _repository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (existingPayment is not null)
        {
            return PaymentMapper.ToDto(existingPayment);
        }

        if (!string.Equals(order.Status, "PendingPayment", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Payment can only be created for an order awaiting payment.");
        }

        var payment = new Payment(
            Guid.NewGuid(),
            order.OrderId,
            order.CustomerId,
            order.TotalAmount,
            order.Currency,
            PaymentStatus.Pending,
            DateTime.UtcNow);

        var createdPayment = await _repository.CreateAsync(payment, cancellationToken);

        return PaymentMapper.ToDto(createdPayment);
    }
}
