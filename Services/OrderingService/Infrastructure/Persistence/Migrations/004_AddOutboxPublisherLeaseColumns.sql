ALTER TABLE OutboxMessages
ADD COLUMN IF NOT EXISTS NextAttemptAtUtc timestamptz NOT NULL DEFAULT now();

ALTER TABLE OutboxMessages
ADD COLUMN IF NOT EXISTS LockId uuid NULL;

ALTER TABLE OutboxMessages
ADD COLUMN IF NOT EXISTS LockedUntilUtc timestamptz NULL;

CREATE INDEX IF NOT EXISTS IX_OutboxMessages_Pending
ON OutboxMessages(NextAttemptAtUtc, OccurredAtUtc)
WHERE ProcessedAtUtc IS NULL;
