using MediatR;
namespace OrderingService.Application.Fulfillment;
public sealed record DispatchShipmentCommand(Guid OrderId, Guid ActorId, string Carrier, string TrackingNumber) : IRequest<ShipmentDto?>;