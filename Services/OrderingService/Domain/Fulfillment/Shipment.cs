namespace OrderingService.Domain.Fulfillment;

public sealed class Shipment
{
    public Guid Id { get; }
    public Guid OrderId { get; }
    public ShipmentStatus Status { get; private set; }
    public string? Carrier { get; private set; }
    public string? TrackingNumber { get; private set; }
    public DateTime CreatedAtUtc { get; }
    public DateTime UpdatedAtUtc { get; private set; }

    public Shipment(Guid id, Guid orderId, ShipmentStatus status, DateTime createdAtUtc, DateTime updatedAtUtc, string? carrier = null, string? trackingNumber = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Shipment id cannot be empty.", nameof(id));
        if (orderId == Guid.Empty) throw new ArgumentException("Order id cannot be empty.", nameof(orderId));
        if (updatedAtUtc < createdAtUtc) throw new ArgumentException("Shipment update time cannot precede creation time.", nameof(updatedAtUtc));

        Id = id;
        OrderId = orderId;
        Status = status;
        Carrier = Normalize(carrier);
        TrackingNumber = Normalize(trackingNumber);
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;

        if ((status is ShipmentStatus.Shipped or ShipmentStatus.Delivered) && (Carrier is null || TrackingNumber is null))
        {
            throw new ArgumentException("Shipped and delivered shipments require carrier and tracking number.");
        }
    }

    public static Shipment Create(Guid orderId, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), orderId, ShipmentStatus.ReadyToShip, createdAtUtc, createdAtUtc);

    public bool Dispatch(string carrier, string trackingNumber, DateTime occurredAtUtc)
    {
        if (Status == ShipmentStatus.Shipped) return false;
        if (Status != ShipmentStatus.ReadyToShip) throw new InvalidOperationException($"Shipment cannot move from {Status} to Shipped.");
        if (occurredAtUtc < UpdatedAtUtc) throw new InvalidOperationException("Shipment event cannot be older than the current shipment state.");

        Carrier = Require(carrier, nameof(carrier));
        TrackingNumber = Require(trackingNumber, nameof(trackingNumber));
        Status = ShipmentStatus.Shipped;
        UpdatedAtUtc = occurredAtUtc;
        return true;
    }

    public bool Deliver(DateTime occurredAtUtc)
    {
        if (Status == ShipmentStatus.Delivered) return false;
        if (Status != ShipmentStatus.Shipped) throw new InvalidOperationException($"Shipment cannot move from {Status} to Delivered.");
        if (occurredAtUtc < UpdatedAtUtc) throw new InvalidOperationException("Shipment event cannot be older than the current shipment state.");

        Status = ShipmentStatus.Delivered;
        UpdatedAtUtc = occurredAtUtc;
        return true;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Require(string value, string parameterName) => Normalize(value) ?? throw new ArgumentException("A value is required.", parameterName);
}