ALTER TABLE CatalogOutboxMessages
    ADD COLUMN IF NOT EXISTS LockId uuid NULL,
    ADD COLUMN IF NOT EXISTS LockedUntilUtc timestamptz NULL;

CREATE INDEX IF NOT EXISTS IX_CatalogOutboxMessages_Locked
ON CatalogOutboxMessages (LockedUntilUtc)
WHERE ProcessedAtUtc IS NULL AND LockedUntilUtc IS NOT NULL;
