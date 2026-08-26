CREATE TABLE IF NOT EXISTS InventoryStockReceipts (
    ReceiptId uuid PRIMARY KEY,
    SourcePurchaseOrderId uuid NOT NULL,
    ReceivedAtUtc timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_InventoryStockReceipts_SourcePurchaseOrderId
ON InventoryStockReceipts (SourcePurchaseOrderId);