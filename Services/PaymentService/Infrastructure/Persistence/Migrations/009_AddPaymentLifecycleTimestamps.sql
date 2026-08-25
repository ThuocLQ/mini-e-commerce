ALTER TABLE Payments
    ADD COLUMN IF NOT EXISTS AuthorizedAtUtc timestamptz NULL,
    ADD COLUMN IF NOT EXISTS CaptureRequestedAtUtc timestamptz NULL,
    ADD COLUMN IF NOT EXISTS CapturedAtUtc timestamptz NULL,
    ADD COLUMN IF NOT EXISTS VoidRequestedAtUtc timestamptz NULL,
    ADD COLUMN IF NOT EXISTS VoidedAtUtc timestamptz NULL,
    ADD COLUMN IF NOT EXISTS RefundRequestedAtUtc timestamptz NULL,
    ADD COLUMN IF NOT EXISTS RefundedAtUtc timestamptz NULL;

-- Preserve the time of legacy successful payments without rewriting their persisted status.
UPDATE Payments
SET CapturedAtUtc = CompletedAtUtc
WHERE Status IN ('Succeeded', 'Captured')
  AND CapturedAtUtc IS NULL
  AND CompletedAtUtc IS NOT NULL;
