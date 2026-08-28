using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Application.Outbox;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.AdvanceFulfillment;

public sealed class AdvanceFulfillmentHandler : IRequestHandler<AdvanceFulfillmentCommand, OrderDto?>
{
    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly IOrderRepository _orderRepository;
    private readonly IOutboxRepository _outboxRepository;

    public AdvanceFulfillmentHandler(
        IOrderingUnitOfWork unitOfWork,
        IOrderRepository orderRepository,
        IOutboxRepository outboxRepository)
    {
        _unitOfWork = unitOfWork;
        _orderRepository = orderRepository;
        _outboxRepository = outboxRepository;
    }

    public Task<OrderDto?> Handle(AdvanceFulfillmentCommand request, CancellationToken cancellationToken)
    {
        if (request.OrderId == Guid.Empty)
        {
            throw new ArgumentException("Order id cannot be empty.", nameof(request.OrderId));
        }

        if (request.TargetStatus is not (OrderStatus.Confirmed or OrderStatus.Shipped or OrderStatus.Delivered))
        {
            throw new ArgumentException("Target status must be Confirmed, Shipped, or Delivered.", nameof(request.TargetStatus));
        }

        return _unitOfWork.ExecuteAsync(async transaction =>
        {
            var order = await _orderRepository.GetByIdAsync(request.OrderId, transaction, cancellationToken);
            if (order is null)
            {
                return null;
            }

            var previousStatus = order.Status;
            if (!order.MoveToFulfillmentStatus(request.TargetStatus))
            {
                return OrderMapper.ToDto(order);
            }

            var updated = await _orderRepository.TryUpdateStatusAsync(
                order.Id,
                order.Status,
                [previousStatus],
                transaction,
                cancellationToken);
            if (!updated)
            {
                throw new InvalidOperationException("Order status changed before the fulfillment transition was applied.");
            }

            await _outboxRepository.AddAsync(
                OutboxMessageFactory.Create(OrderIntegrationEventFactory.CreateOrderStatusChanged(order, previousStatus)),
                transaction,
                cancellationToken);
            await _outboxRepository.AddAsync(
                OutboxMessageFactory.CreateKafka(OrderIntegrationEventFactory.CreateOrderProjectionStatusChanged(order, previousStatus)),
                transaction,
                cancellationToken);

            return OrderMapper.ToDto(order);
        }, cancellationToken);
    }
}