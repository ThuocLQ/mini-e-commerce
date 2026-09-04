using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Application.Outbox;
using OrderingService.Domain.Fulfillment;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Fulfillment;

public sealed class DispatchShipmentHandler(
    IOrderingUnitOfWork unitOfWork,
    IOrderRepository orders,
    IShipmentRepository shipments,
    IOutboxRepository outbox) : IRequestHandler<DispatchShipmentCommand, ShipmentDto?>
{
    public Task<ShipmentDto?> Handle(DispatchShipmentCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async transaction =>
        {
            var shipment = await shipments.GetByOrderIdAsync(request.OrderId, transaction, cancellationToken);
            if (shipment is null)
            {
                return null;
            }

            if (shipment.Status == ShipmentStatus.Shipped)
            {
                return ShipmentMapper.ToDto(shipment);
            }

            var order = await orders.GetByIdAsync(request.OrderId, transaction, cancellationToken)
                ?? throw new InvalidOperationException("Shipment order is missing.");
            var previousShipmentStatus = shipment.Status;

            shipment.Dispatch(request.Carrier, request.TrackingNumber, DateTime.UtcNow);
            if (!order.MoveToFulfillmentStatus(OrderStatus.Shipped))
            {
                throw new InvalidOperationException("Order could not be marked shipped.");
            }

            if (!await shipments.UpdateAsync(shipment, previousShipmentStatus, transaction, cancellationToken) ||
                !await orders.TryUpdateStatusAsync(order.Id, order.Status, [OrderStatus.Confirmed], transaction, cancellationToken))
            {
                throw new InvalidOperationException("Shipment changed before dispatch.");
            }

            await outbox.AddAsync(
                OutboxMessageFactory.Create(OrderIntegrationEventFactory.CreateOrderStatusChanged(order, OrderStatus.Confirmed)),
                transaction,
                cancellationToken);
            await outbox.AddAsync(
                OutboxMessageFactory.CreateKafka(OrderIntegrationEventFactory.CreateOrderProjectionStatusChanged(order, OrderStatus.Confirmed)),
                transaction,
                cancellationToken);
            await shipments.AddHistoryAsync(
                ShipmentStatusHistory.Create(shipment.Id, previousShipmentStatus, shipment.Status, request.ActorId, "Shipment dispatched.", DateTime.UtcNow),
                transaction,
                cancellationToken);

            return ShipmentMapper.ToDto(shipment);
        }, cancellationToken);
}
