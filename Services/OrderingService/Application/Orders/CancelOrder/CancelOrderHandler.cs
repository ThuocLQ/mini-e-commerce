using BuildingBlocks.Contracts.Events.Discounts;
using BuildingBlocks.Contracts.Events.Inventory;
using BuildingBlocks.Contracts.Events.Payments;
using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Application.Outbox;
using OrderingService.Domain.OrderPaymentSagas;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.CancelOrder;

public sealed class CancelOrderHandler : IRequestHandler<CancelOrderCommand, OrderDto?>
{
    private const int MaxReasonLength = 500;

    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderPaymentSagaRepository _sagaRepository;
    private readonly IOutboxRepository _outboxRepository;

    public CancelOrderHandler(
        IOrderingUnitOfWork unitOfWork,
        IOrderRepository orderRepository,
        IOrderPaymentSagaRepository sagaRepository,
        IOutboxRepository outboxRepository)
    {
        _unitOfWork = unitOfWork;
        _orderRepository = orderRepository;
        _sagaRepository = sagaRepository;
        _outboxRepository = outboxRepository;
    }

    public async Task<OrderDto?> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.OrderId == Guid.Empty)
        {
            throw new ArgumentException("Order id cannot be empty.", nameof(request.OrderId));
        }

        if (request.CustomerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id cannot be empty.", nameof(request.CustomerId));
        }

        var reason = NormalizeReason(request.Reason);
        var cancellationEventId = Guid.NewGuid();

        return await _unitOfWork.ExecuteAsync(async transaction =>
        {
            var order = await _orderRepository.GetByIdAsync(request.OrderId, transaction, cancellationToken);
            if (order is null || order.CustomerId != request.CustomerId)
            {
                return null;
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                return OrderMapper.ToDto(order);
            }

            var previousStatus = order.Status;
            order.CancelBeforeFulfillment();

            var updated = await _orderRepository.TryUpdateStatusAsync(
                order.Id,
                order.Status,
                [previousStatus],
                transaction,
                cancellationToken);
            if (!updated)
            {
                throw new InvalidOperationException("Order status changed before cancellation was applied.");
            }

            await AddOrderCancellationEventsAsync(order, previousStatus, reason, cancellationEventId, transaction, cancellationToken);

            var saga = await _sagaRepository.GetByOrderIdAsync(order.Id, transaction, cancellationToken);
            if (saga is not null)
            {
                var previousSagaState = saga.State;
                ApplyCancellationToSaga(saga, cancellationEventId, reason);

                if (saga.State != previousSagaState)
                {
                    var sagaStateChanged = OrderIntegrationEventFactory.CreatePaymentSagaStateChanged(
                        saga,
                        previousSagaState,
                        cancellationEventId.ToString("D"));
                    await _outboxRepository.AddAsync(OutboxMessageFactory.Create(sagaStateChanged), transaction, cancellationToken);

                    if (saga.State == OrderPaymentSagaState.VoidRequested)
                    {
                        await _outboxRepository.AddAsync(OutboxMessageFactory.Create(new PaymentVoidRequestedIntegrationEvent
                        {
                            PaymentId = saga.PaymentId,
                            OrderId = order.Id,
                            CustomerId = order.CustomerId,
                            Amount = order.TotalAmount,
                            Currency = order.Currency,
                            Reason = reason,
                            CorrelationId = order.Id.ToString("N"),
                            CausationId = cancellationEventId.ToString("D")
                        }), transaction, cancellationToken);
                    }
                }

                await _sagaRepository.UpsertAsync(saga, transaction, cancellationToken);
            }

            return OrderMapper.ToDto(order);
        }, cancellationToken);
    }

    private async Task AddOrderCancellationEventsAsync(
        Order order,
        OrderStatus previousStatus,
        string reason,
        Guid causationEventId,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await _outboxRepository.AddAsync(
            OutboxMessageFactory.Create(OrderIntegrationEventFactory.CreateOrderStatusChanged(order, previousStatus)),
            transaction,
            cancellationToken);
        await _outboxRepository.AddAsync(
            OutboxMessageFactory.CreateKafka(OrderIntegrationEventFactory.CreateOrderProjectionStatusChanged(order, previousStatus)),
            transaction,
            cancellationToken);
        await _outboxRepository.AddAsync(OutboxMessageFactory.Create(new InventoryReleaseRequestedIntegrationEvent
        {
            OrderId = order.Id,
            Reason = reason,
            CorrelationId = order.Id.ToString("N"),
            CausationId = causationEventId.ToString("D")
        }), transaction, cancellationToken);

        if (order.DiscountReservationId is { } reservationId)
        {
            await _outboxRepository.AddAsync(OutboxMessageFactory.Create(new PromotionReleaseRequestedIntegrationEvent
            {
                ReservationId = reservationId,
                OrderId = order.Id,
                Reason = reason,
                CorrelationId = order.Id.ToString("N"),
                CausationId = causationEventId.ToString("D")
            }), transaction, cancellationToken);
        }
    }

    private static void ApplyCancellationToSaga(OrderPaymentSaga saga, Guid eventId, string reason)
    {
        if (saga.State is OrderPaymentSagaState.PaymentAuthorized or OrderPaymentSagaState.CaptureRequested)
        {
            saga.MarkVoidRequested(eventId, DateTime.UtcNow, reason);
            return;
        }

        if (saga.State is not (OrderPaymentSagaState.OrderCancelled or OrderPaymentSagaState.TimedOut))
        {
            saga.MarkOrderCancelled(eventId, DateTime.UtcNow, reason);
        }
    }

    private static string NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "Cancelled by customer before fulfillment.";
        }

        reason = reason.Trim();
        if (reason.Length > MaxReasonLength)
        {
            throw new ArgumentException($"Cancellation reason cannot exceed {MaxReasonLength} characters.", nameof(reason));
        }

        return reason;
    }
}