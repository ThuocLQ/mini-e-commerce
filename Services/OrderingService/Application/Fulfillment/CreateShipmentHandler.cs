using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Application.Outbox;
using OrderingService.Domain.Fulfillment;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Fulfillment;

public sealed class CreateShipmentHandler(
    IOrderingUnitOfWork unitOfWork,
    IOrderRepository orders,
    IShipmentRepository shipments,
    IOutboxRepository outbox) : IRequestHandler<CreateShipmentCommand, ShipmentDto?>
{
    public Task<ShipmentDto?> Handle(CreateShipmentCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async transaction =>
        {
            var order = await orders.GetByIdAsync(request.OrderId, transaction, cancellationToken);
            if (order is null)
            {
                return null;
            }

            var existingShipment = await shipments.GetByOrderIdAsync(order.Id, transaction, cancellationToken);
            if (existingShipment is not null)
            {
                return ShipmentMapper.ToDto(existingShipment);
            }

            if (order.Status != OrderStatus.Paid)
            {
                throw new InvalidOperationException("Only paid orders can enter fulfillment.");
            }

            var shipment = Shipment.Create(order.Id, DateTime.UtcNow);
            if (!order.MoveToFulfillmentStatus(OrderStatus.Confirmed))
            {
                throw new InvalidOperationException("Order could not enter fulfillment.");
            }

            if (!await orders.TryUpdateStatusAsync(order.Id, order.Status, [OrderStatus.Paid], transaction, cancellationToken))
            {
                throw new InvalidOperationException("Order changed before shipment creation.");
            }

            await shipments.CreateAsync(shipment, transaction, cancellationToken);
            await outbox.AddAsync(
                OutboxMessageFactory.Create(OrderIntegrationEventFactory.CreateOrderStatusChanged(order, OrderStatus.Paid)),
                transaction,
                cancellationToken);
            await outbox.AddAsync(
                OutboxMessageFactory.CreateKafka(OrderIntegrationEventFactory.CreateOrderProjectionStatusChanged(order, OrderStatus.Paid)),
                transaction,
                cancellationToken);
            await shipments.AddHistoryAsync(
                ShipmentStatusHistory.Create(shipment.Id, null, shipment.Status, request.ActorId, "Shipment created for a paid order.", DateTime.UtcNow),
                transaction,
                cancellationToken);

            return ShipmentMapper.ToDto(shipment);
        }, cancellationToken);
}
