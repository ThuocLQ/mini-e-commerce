using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Application.Outbox;
using OrderingService.Domain.OrderPaymentSagas;

namespace OrderingService.Application.OrderPaymentSagas.ApplyInventorySettlement;

public sealed class ApplyInventorySettlementEventHandler
    : IRequestHandler<ApplyInventorySettlementEventCommand, OrderPaymentSagaDto?>
{
    internal const string ConsumerName = "OrderingService.InventorySettlement";

    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderPaymentSagaRepository _sagaRepository;
    private readonly IInboxRepository _inboxRepository;
    private readonly IOutboxRepository _outboxRepository;

    public ApplyInventorySettlementEventHandler(
        IOrderingUnitOfWork unitOfWork,
        IOrderRepository orderRepository,
        IOrderPaymentSagaRepository sagaRepository,
        IInboxRepository inboxRepository,
        IOutboxRepository outboxRepository)
    {
        _unitOfWork = unitOfWork;
        _orderRepository = orderRepository;
        _sagaRepository = sagaRepository;
        _inboxRepository = inboxRepository;
        _outboxRepository = outboxRepository;
    }

    public async Task<OrderPaymentSagaDto?> Handle(
        ApplyInventorySettlementEventCommand request,
        CancellationToken cancellationToken)
    {
        if (request.EventId == Guid.Empty)
        {
            throw new ArgumentException("Event id cannot be empty.", nameof(request.EventId));
        }

        if (request.OrderId == Guid.Empty)
        {
            throw new ArgumentException("Order id cannot be empty.", nameof(request.OrderId));
        }

        var saga = await _unitOfWork.ExecuteAsync(async transaction =>
        {
            var order = await _orderRepository.GetByIdAsync(request.OrderId, transaction, cancellationToken);
            if (order is null)
            {
                return null;
            }

            var currentSaga = await _sagaRepository.GetByOrderIdAsync(order.Id, transaction, cancellationToken)
                ?? throw new InvalidOperationException($"Payment saga for order '{order.Id}' was not found.");

            if (!await _inboxRepository.TryRecordAsync(request.EventId, ConsumerName, transaction, cancellationToken))
            {
                return currentSaga;
            }

            var previousState = currentSaga.State;
            var updatedAtUtc = DateTime.UtcNow;

            ApplySettlement(request, currentSaga, updatedAtUtc);

            if (currentSaga.State != previousState)
            {
                var stateChangedEvent = OrderIntegrationEventFactory.CreatePaymentSagaStateChanged(
                    currentSaga,
                    previousState,
                    request.EventId.ToString("D"));
                await _outboxRepository.AddAsync(OutboxMessageFactory.Create(stateChangedEvent), transaction, cancellationToken);
            }

            await _sagaRepository.UpsertAsync(currentSaga, transaction, cancellationToken);
            return currentSaga;
        }, cancellationToken);

        return saga is null ? null : OrderPaymentSagaMapper.ToDto(saga);
    }

    private static void ApplySettlement(
        ApplyInventorySettlementEventCommand request,
        OrderPaymentSaga saga,
        DateTime updatedAtUtc)
    {
        switch (request.EventType)
        {
            case OrderInventorySettlementEventType.InventoryCommitted:
                if (saga.State == OrderPaymentSagaState.OrderPaid)
                {
                    saga.MarkInventoryCommitted(request.EventId, updatedAtUtc);
                    return;
                }

                saga.RecordIgnoredEvent(
                    request.EventId,
                    updatedAtUtc,
                    "InventoryCommitted was received after the payment saga had already reached a terminal state.");
                return;

            case OrderInventorySettlementEventType.InventoryReleased:
                if (saga.State is OrderPaymentSagaState.OrderPaid or OrderPaymentSagaState.InventoryCommitted)
                {
                    saga.MarkCompensationRequired(
                        request.EventId,
                        updatedAtUtc,
                        string.IsNullOrWhiteSpace(request.Reason)
                            ? "Inventory reservation was released after payment succeeded."
                            : request.Reason);
                    return;
                }

                saga.RecordIgnoredEvent(request.EventId, updatedAtUtc, request.Reason);
                return;

            default:
                throw new InvalidOperationException($"Unsupported inventory settlement event type '{request.EventType}'.");
        }
    }
}
