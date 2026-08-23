CREATE TABLE IF NOT EXISTS InventoryCommandReceipts (
    EventId uuid PRIMARY KEY,
    CommandType text NOT NULL,
    ReceivedAtUtc timestamptz NOT NULL,
    CHECK (CommandType IN ('Committed', 'Released'))
);

CREATE INDEX IF NOT EXISTS IX_InventoryCommandReceipts_ReceivedAtUtc
ON InventoryCommandReceipts (ReceivedAtUtc);
