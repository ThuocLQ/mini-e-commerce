ALTER TABLE OrderPaymentSagas
    ADD COLUMN IF NOT EXISTS ExpectedInventoryCommandEventId uuid NULL;
