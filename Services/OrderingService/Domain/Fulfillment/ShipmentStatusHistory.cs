namespace OrderingService.Domain.Fulfillment;

public sealed record ShipmentStatusHistory(
    Guid Id,
    Guid ShipmentId,
    ShipmentStatus? PreviousStatus,
    ShipmentStatus CurrentStatus,
    Guid ActorId,
    string Reason,
    DateTime OccurredAtUtc)
{
    public static ShipmentStatusHistory Create(
        Guid shipmentId,
        ShipmentStatus? previousStatus,
        ShipmentStatus currentStatus,
        Guid actorId,
        string reason,
        DateTime occurredAtUtc)
    {
        if (shipmentId == Guid.Empty) throw new ArgumentException("Shipment id cannot be empty.", nameof(shipmentId));
        if (actorId == Guid.Empty) throw new ArgumentException("Actor id cannot be empty.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));
        return new ShipmentStatusHistory(Guid.NewGuid(), shipmentId, previousStatus, currentStatus, actorId, reason.Trim(), occurredAtUtc);
    }
}