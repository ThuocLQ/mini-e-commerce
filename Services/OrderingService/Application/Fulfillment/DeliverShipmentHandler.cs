using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Application.Outbox;
using OrderingService.Domain.Fulfillment;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Fulfillment;

public sealed class DeliverShipmentHandler(
    IOrderingUnitOfWork unitOfWork,
    IOrderRepository orders,
    IShipmentRepository shipments,
    IOutboxRepository outbox) : IRequestHandler<DeliverShipmentCommand, ShipmentDto?>
{
    public Task<ShipmentDto?> Handle(DeliverShipmentCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async transaction =>
        {
            var shipment = await shipments.GetByOrderIdAsync(request.OrderId, transaction, cancellationToken);
            if (shipment is null)
            {
                return null;
            }

            if (shipment.Status == ShipmentStatus.Delivered)
            {
                return ShipmentMapper.ToDto(shipment);
            }

            var order = await orders.GetByIdAsync(request.OrderId, transaction, cancellationToken)
                ?? throw new InvalidOperationException("Shipment order is missing.");
            var previousShipmentStatus = shipment.Status;

            shipment.Deliver(DateTime.UtcNow);
            if (!order.MoveToFulfillmentStatus(OrderStatus.Delivered))
            {
                throw new InvalidOperationException("Order could not be marked delivered.");
            }

            if (!await shipments.UpdateAsync(shipment, previousShipmentStatus, transaction, cancellationToken) ||
                !await orders.TryUpdateStatusAsync(order.Id, order.Status, [OrderStatus.Shipped], transaction, cancellationToken))
            {
                throw new InvalidOperationException("Shipment changed before delivery.");
            }

            await outbox.AddAsync(
                OutboxMessageFactory.Create(OrderIntegrationEventFactory.CreateOrderStatusChanged(order, OrderStatus.Shipped)),
                transaction,
                cancellationToken);
            await outbox.AddAsync(
                OutboxMessageFactory.CreateKafka(OrderIntegrationEventFactory.CreateOrderProjectionStatusChanged(order, OrderStatus.Shipped)),
                transaction,
                cancellationToken);
            await shipments.AddHistoryAsync(
                ShipmentStatusHistory.Create(shipment.Id, previousShipmentStatus, shipment.Status, request.ActorId, "Shipment delivered.", DateTime.UtcNow),
                transaction,
                cancellationToken);

            return ShipmentMapper.ToDto(shipment);
        }, cancellationToken);
}
