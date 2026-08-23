using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Application.Outbox;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.ApplyPaymentResult;

public sealed class ApplyOrderPaymentResultHandler : IRequestHandler<ApplyOrderPaymentResultCommand, OrderDto?>
{
    private readonly IOrderRepository _repository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IOrderingUnitOfWork _unitOfWork;

    public ApplyOrderPaymentResultHandler(
        IOrderRepository repository,
        IOutboxRepository outboxRepository,
        IOrderingUnitOfWork unitOfWork)
    {
        _repository = repository;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderDto?> Handle(ApplyOrderPaymentResultCommand request, CancellationToken cancellationToken)
    {
        var result = await _unitOfWork.ExecuteAsync(async transaction =>
        {
            var order = await _repository.GetByIdAsync(request.OrderId, transaction, cancellationToken);
            if (order is null)
            {
                return new ApplyPaymentResultOutcome(null, false);
            }

            var previousStatus = order.Status;
            var changed = request.Result switch
            {
                OrderPaymentResult.Succeeded => order.MarkPaid(),
                OrderPaymentResult.Failed => order.MarkPaymentFailed(),
                _ => throw new InvalidOperationException($"Unsupported payment result '{request.Result}'.")
            };

            if (!changed)
            {
                return new ApplyPaymentResultOutcome(order, false);
            }

            var updated = await _repository.TryUpdateStatusAsync(
                order.Id,
                order.Status,
                [previousStatus],
                transaction,
                cancellationToken);
            if (!updated)
            {
                return new ApplyPaymentResultOutcome(null, true);
            }

            var statusChangedEvent = OrderIntegrationEventFactory.CreateOrderStatusChanged(order, previousStatus);
            var projectionEvent = OrderIntegrationEventFactory.CreateOrderProjectionStatusChanged(order, previousStatus);
            await _outboxRepository.AddAsync(OutboxMessageFactory.Create(statusChangedEvent), transaction, cancellationToken);
            await _outboxRepository.AddAsync(OutboxMessageFactory.CreateKafka(projectionEvent), transaction, cancellationToken);

            return new ApplyPaymentResultOutcome(order, false);
        }, cancellationToken);

        if (result.Order is not null)
        {
            return OrderMapper.ToDto(result.Order);
        }

        if (!result.RequiresConflictRead)
        {
            return null;
        }

        var currentOrder = await _repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (currentOrder is null)
        {
            return null;
        }

        var requestedStatus = request.Result == OrderPaymentResult.Succeeded
            ? OrderStatus.Paid
            : OrderStatus.PaymentFailed;

        if (currentOrder.Status == requestedStatus ||
            request.Result == OrderPaymentResult.Failed && currentOrder.Status == OrderStatus.Paid)
        {
            return OrderMapper.ToDto(currentOrder);
        }

        throw new InvalidOperationException("Order status changed before the payment result was applied. Retry the operation.");
    }

    private sealed record ApplyPaymentResultOutcome(Order? Order, bool RequiresConflictRead);
}
