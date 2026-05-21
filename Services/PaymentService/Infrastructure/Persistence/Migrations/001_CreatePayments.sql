CREATE TABLE IF NOT EXISTS Payments (
    Id uuid PRIMARY KEY,
    OrderId uuid NOT NULL,
    CustomerId uuid NOT NULL,
    Amount numeric(18, 2) NOT NULL,
    Currency text NOT NULL,
    Status text NOT NULL,
    ProviderTransactionId text NULL,
    FailureReason text NULL,
    CreatedAtUtc timestamptz NOT NULL,
    CompletedAtUtc timestamptz NULL
);

CREATE INDEX IF NOT EXISTS IX_Payments_OrderId
ON Payments(OrderId);
