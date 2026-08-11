ALTER TABLE Products ADD COLUMN IF NOT EXISTS StockQuantity integer NOT NULL DEFAULT 0;
ALTER TABLE Products ADD COLUMN IF NOT EXISTS ReservedQuantity integer NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS InventoryReservations (
    OrderId uuid PRIMARY KEY,
    Status text NOT NULL,
    ExpiresAtUtc timestamptz NOT NULL,
    CreatedAtUtc timestamptz NOT NULL,
    UpdatedAtUtc timestamptz NOT NULL,
    CHECK (Status IN ('Reserved', 'Released', 'Committed'))
);

CREATE TABLE IF NOT EXISTS InventoryReservationItems (
    OrderId uuid NOT NULL REFERENCES InventoryReservations(OrderId),
    ProductId text NOT NULL REFERENCES Products(Id),
    Quantity integer NOT NULL CHECK (Quantity > 0),
    PRIMARY KEY (OrderId, ProductId)
);

CREATE INDEX IF NOT EXISTS IX_InventoryReservations_Expiry
ON InventoryReservations (ExpiresAtUtc) WHERE Status = 'Reserved';
