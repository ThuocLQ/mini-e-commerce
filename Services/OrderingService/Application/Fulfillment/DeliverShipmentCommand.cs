using MediatR;
namespace OrderingService.Application.Fulfillment;
public sealed record DeliverShipmentCommand(Guid OrderId, Guid ActorId) : IRequest<ShipmentDto?>;