using OrderingService.Domain.Fulfillment;
namespace OrderingService.Application.Fulfillment;
public sealed record ShipmentDto(Guid Id, Guid OrderId, string Status, string? Carrier, string? TrackingNumber, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public static class ShipmentMapper { public static ShipmentDto ToDto(Shipment shipment) => new(shipment.Id, shipment.OrderId, shipment.Status.ToString(), shipment.Carrier, shipment.TrackingNumber, shipment.CreatedAtUtc, shipment.UpdatedAtUtc); }