using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.OrderPaymentSagas;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.OrderPaymentSagas.ApplyPaymentEvent;

public sealed class ApplyPaymentSagaEventHandler : IRequestHandler<ApplyPaymentSagaEventCommand, OrderPaymentSagaDto?>
{
    private static readonly TimeSpan DefaultPaymentTimeout = TimeSpan.FromMinutes(30);

    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderPaymentSagaRepository _sagaRepository;

    public ApplyPaymentSagaEventHandler(
        IOrderingUnitOfWork unitOfWork,
        IOrderRepository orderRepository,
        IOrderPaymentSagaRepository sagaRepository)
    {
        _unitOfWork = unitOfWork;
        _orderRepository = orderRepository;
        _sagaRepository = sagaRepository;
    }

    public async Task<OrderPaymentSagaDto?> Handle(
        ApplyPaymentSagaEventCommand request,
        CancellationToken cancellationToken)
    {
        if (request.EventId == Guid.Empty)
        {
            throw new ArgumentException("Event id cannot be empty.", nameof(request.EventId));
        }

        if (request.PaymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment id cannot be empty.", nameof(request.PaymentId));
        }

        var saga = await _unitOfWork.ExecuteAsync(async transaction =>
        {
            var order = await _orderRepository.GetByIdAsync(request.OrderId, transaction, cancellationToken);
            if (order is null)
            {
                return null;
            }

            var currentSaga = await _sagaRepository.GetByOrderIdAsync(order.Id, transaction, cancellationToken)
                ?? OrderPaymentSaga.Start(order.Id, request.PaymentId, DateTime.UtcNow, DefaultPaymentTimeout);

            if (currentSaga.HasProcessed(request.EventId))
            {
                return currentSaga;
            }

            await ApplyEventAsync(request, order, currentSaga, transaction, cancellationToken);

            await _sagaRepository.UpsertAsync(currentSaga, transaction, cancellationToken);

            return currentSaga;
        }, cancellationToken);

        return saga is null ? null : OrderPaymentSagaMapper.ToDto(saga);
    }

    private async Task ApplyEventAsync(
        ApplyPaymentSagaEventCommand request,
        Order order,
        OrderPaymentSaga saga,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var updatedAtUtc = DateTime.UtcNow;

        switch (request.EventType)
        {
            case OrderPaymentSagaEventType.PaymentSucceeded:
                await ApplyPaymentSucceededAsync(request, order, saga, updatedAtUtc, transaction, cancellationToken);
                break;
            case OrderPaymentSagaEventType.PaymentFailed:
                await ApplyPaymentFailedAsync(request, order, saga, updatedAtUtc, transaction, cancellationToken);
                break;
            case OrderPaymentSagaEventType.PaymentTimedOut:
                await ApplyPaymentTimedOutAsync(request, order, saga, updatedAtUtc, transaction, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported payment saga event type '{request.EventType}'.");
        }
    }

    private async Task ApplyPaymentSucceededAsync(
        ApplyPaymentSagaEventCommand request,
        Order order,
        OrderPaymentSaga saga,
        DateTime updatedAtUtc,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (saga.State is OrderPaymentSagaState.OrderPaid or OrderPaymentSagaState.CompensationRequired ||
            order.Status == OrderStatus.Paid)
        {
            saga.RecordIgnoredEvent(request.EventId, updatedAtUtc);
            return;
        }

        if (saga.State is OrderPaymentSagaState.OrderCancelled or OrderPaymentSagaState.TimedOut ||
            order.Status == OrderStatus.Cancelled)
        {
            saga.MarkCompensationRequired(
                request.EventId,
                updatedAtUtc,
                "Payment succeeded after the order was cancelled or timed out.");
            return;
        }

        var previousStatus = order.Status;
        if (order.MarkPaid())
        {
            var updated = await _orderRepository.TryUpdateStatusAsync(
                order.Id,
                order.Status,
                [previousStatus],
                transaction,
                cancellationToken);

            if (!updated)
            {
                throw new InvalidOperationException("Order status changed before PaymentSucceeded was applied.");
            }
        }

        saga.MarkOrderPaid(request.EventId, updatedAtUtc);
    }

    private async Task ApplyPaymentFailedAsync(
        ApplyPaymentSagaEventCommand request,
        Order order,
        OrderPaymentSaga saga,
        DateTime updatedAtUtc,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (saga.State is OrderPaymentSagaState.OrderPaid or OrderPaymentSagaState.CompensationRequired ||
            order.Status == OrderStatus.Paid)
        {
            saga.RecordIgnoredEvent(
                request.EventId,
                updatedAtUtc,
                "Late PaymentFailed ignored because the order is already paid.");
            return;
        }

        if (saga.State is OrderPaymentSagaState.OrderCancelled or OrderPaymentSagaState.TimedOut)
        {
            saga.RecordIgnoredEvent(request.EventId, updatedAtUtc, request.FailureReason);
            return;
        }

        var previousStatus = order.Status;
        if (order.Cancel())
        {
            var updated = await _orderRepository.TryUpdateStatusAsync(
                order.Id,
                order.Status,
                [previousStatus],
                transaction,
                cancellationToken);

            if (!updated)
            {
                throw new InvalidOperationException("Order status changed before PaymentFailed was applied.");
            }
        }

        saga.MarkOrderCancelled(
            request.EventId,
            updatedAtUtc,
            string.IsNullOrWhiteSpace(request.FailureReason) ? "Payment failed." : request.FailureReason);
    }

    private async Task ApplyPaymentTimedOutAsync(
        ApplyPaymentSagaEventCommand request,
        Order order,
        OrderPaymentSaga saga,
        DateTime updatedAtUtc,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (saga.State is OrderPaymentSagaState.OrderPaid or OrderPaymentSagaState.CompensationRequired ||
            order.Status == OrderStatus.Paid)
        {
            saga.RecordIgnoredEvent(
                request.EventId,
                updatedAtUtc,
                "Payment timeout ignored because the order is already paid.");
            return;
        }

        if (saga.State is OrderPaymentSagaState.OrderCancelled or OrderPaymentSagaState.TimedOut)
        {
            saga.RecordIgnoredEvent(request.EventId, updatedAtUtc);
            return;
        }

        var previousStatus = order.Status;
        if (order.Cancel())
        {
            var updated = await _orderRepository.TryUpdateStatusAsync(
                order.Id,
                order.Status,
                [previousStatus],
                transaction,
                cancellationToken);

            if (!updated)
            {
                throw new InvalidOperationException("Order status changed before PaymentTimedOut was applied.");
            }
        }

        saga.MarkTimedOut(request.EventId, updatedAtUtc, "Payment timed out.");
    }
}
