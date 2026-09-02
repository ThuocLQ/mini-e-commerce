using MediatR;
namespace OrderingService.Application.Fulfillment;
public sealed record CreateShipmentCommand(Guid OrderId, Guid ActorId) : IRequest<ShipmentDto?>;