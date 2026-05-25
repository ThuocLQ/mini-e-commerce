CREATE TABLE IF NOT EXISTS OutboxMessages (
    Id uuid PRIMARY KEY,
    OccurredAtUtc timestamptz NOT NULL,
    Type text NOT NULL,
    Content jsonb NOT NULL,
    NextAttemptAtUtc timestamptz NOT NULL,
    ProcessedAtUtc timestamptz NULL,
    RetryCount integer NOT NULL DEFAULT 0,
    LastError text NULL,
    LockId uuid NULL,
    LockedUntilUtc timestamptz NULL
);

CREATE INDEX IF NOT EXISTS IX_OutboxMessages_Pending
ON OutboxMessages(NextAttemptAtUtc, OccurredAtUtc)
WHERE ProcessedAtUtc IS NULL;

CREATE INDEX IF NOT EXISTS IX_OutboxMessages_ProcessedAtUtc
ON OutboxMessages(ProcessedAtUtc);
