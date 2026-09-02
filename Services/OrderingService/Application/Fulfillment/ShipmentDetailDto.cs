namespace OrderingService.Application.Fulfillment;
public sealed record ShipmentHistoryDto(Guid Id,string? PreviousStatus,string CurrentStatus,Guid ActorId,string Reason,DateTime OccurredAtUtc);
public sealed record ShipmentDetailDto(ShipmentDto Shipment,IReadOnlyList<ShipmentHistoryDto> History);