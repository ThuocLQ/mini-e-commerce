ALTER TABLE Products
    ADD COLUMN IF NOT EXISTS InventorySnapshotUpdatedAtUtc timestamptz NULL;
