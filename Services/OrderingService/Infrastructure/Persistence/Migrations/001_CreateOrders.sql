CREATE TABLE IF NOT EXISTS Orders (
    Id uuid PRIMARY KEY,
    CustomerId uuid NOT NULL,
    CreatedAtUtc timestamptz NOT NULL,
    Status text NOT NULL,
    TotalAmount numeric(18, 2) NOT NULL,
    IdempotencyKey text NULL
);

CREATE TABLE IF NOT EXISTS OrderItems (
    Id uuid PRIMARY KEY,
    OrderId uuid NOT NULL REFERENCES Orders(Id) ON DELETE CASCADE,
    ProductId uuid NOT NULL,
    ProductName text NOT NULL,
    UnitPrice numeric(18, 2) NOT NULL,
    Quantity integer NOT NULL,
    TotalPrice numeric(18, 2) NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_OrderItems_OrderId
ON OrderItems(OrderId);

CREATE INDEX IF NOT EXISTS IX_Orders_CustomerId
ON Orders(CustomerId);

CREATE UNIQUE INDEX IF NOT EXISTS IX_Orders_CustomerId_IdempotencyKey
ON Orders(CustomerId, IdempotencyKey)
WHERE IdempotencyKey IS NOT NULL;
