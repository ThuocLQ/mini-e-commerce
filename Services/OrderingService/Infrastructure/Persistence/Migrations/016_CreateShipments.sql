CREATE TABLE IF NOT EXISTS Shipments (
    Id uuid PRIMARY KEY,
    OrderId uuid NOT NULL UNIQUE,
    Status varchar(32) NOT NULL,
    Carrier varchar(100) NULL,
    TrackingNumber varchar(160) NULL,
    CreatedAtUtc timestamptz NOT NULL,
    UpdatedAtUtc timestamptz NOT NULL,
    CONSTRAINT CK_Shipments_Status CHECK (Status IN ('ReadyToShip', 'Shipped', 'Delivered')),
    CONSTRAINT CK_Shipments_Tracking CHECK (
        (Status = 'ReadyToShip' AND Carrier IS NULL AND TrackingNumber IS NULL)
        OR (Status IN ('Shipped', 'Delivered') AND Carrier IS NOT NULL AND TrackingNumber IS NOT NULL)
    )
);

CREATE INDEX IF NOT EXISTS IX_Shipments_Status_UpdatedAtUtc ON Shipments (Status, UpdatedAtUtc DESC);

CREATE TABLE IF NOT EXISTS ShipmentStatusHistory (
    Id uuid PRIMARY KEY,
    ShipmentId uuid NOT NULL REFERENCES Shipments(Id) ON DELETE CASCADE,
    PreviousStatus varchar(32) NULL,
    CurrentStatus varchar(32) NOT NULL,
    ActorId uuid NOT NULL,
    Reason varchar(500) NOT NULL,
    OccurredAtUtc timestamptz NOT NULL,
    CONSTRAINT CK_ShipmentStatusHistory_Status CHECK (CurrentStatus IN ('ReadyToShip', 'Shipped', 'Delivered'))
);

CREATE INDEX IF NOT EXISTS IX_ShipmentStatusHistory_ShipmentId_OccurredAtUtc ON ShipmentStatusHistory (ShipmentId, OccurredAtUtc DESC);