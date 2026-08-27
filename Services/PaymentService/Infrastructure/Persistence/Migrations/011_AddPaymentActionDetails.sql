ALTER TABLE Payments
    ADD COLUMN IF NOT EXISTS Provider text NULL,
    ADD COLUMN IF NOT EXISTS ProviderSessionId text NULL,
    ADD COLUMN IF NOT EXISTS PaymentActionIdempotencyKey text NULL,
    ADD COLUMN IF NOT EXISTS PaymentActionRequestHash text NULL,
    ADD COLUMN IF NOT EXISTS PaymentActionExpiresAtUtc timestamptz NULL;

CREATE UNIQUE INDEX IF NOT EXISTS UX_Payments_Customer_ActionIdempotencyKey
ON Payments(CustomerId, PaymentActionIdempotencyKey)
WHERE PaymentActionIdempotencyKey IS NOT NULL;
