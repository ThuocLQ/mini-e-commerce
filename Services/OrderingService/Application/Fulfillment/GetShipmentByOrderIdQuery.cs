using MediatR;
namespace OrderingService.Application.Fulfillment;
public sealed record GetShipmentByOrderIdQuery(Guid OrderId) : IRequest<ShipmentDetailDto?>;