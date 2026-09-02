ALTER TABLE Payments
    ADD COLUMN IF NOT EXISTS ProviderCheckoutUrl text NULL;