CREATE INDEX IF NOT EXISTS IX_PaymentOutboxMessages_Processing_LockedUntilUtc
ON PaymentOutboxMessages (LockedUntilUtc)
WHERE Status = 'Processing';
