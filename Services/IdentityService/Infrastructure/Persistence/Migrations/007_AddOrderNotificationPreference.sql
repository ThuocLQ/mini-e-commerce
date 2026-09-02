ALTER TABLE Users
    ADD COLUMN IF NOT EXISTS ReceiveOrderUpdates boolean NOT NULL DEFAULT true;