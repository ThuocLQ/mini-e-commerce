CREATE TABLE IF NOT EXISTS InventoryOutboxMessages (
    Id uuid PRIMARY KEY,
    OccurredAtUtc timestamptz NOT NULL,
    Type text NOT NULL,
    Content jsonb NOT NULL,
    CorrelationId text NULL,
    CausationId text NULL,
    NextAttemptAtUtc timestamptz NOT NULL,
    ProcessedAtUtc timestamptz NULL,
    RetryCount integer NOT NULL DEFAULT 0,
    LastError text NULL,
    LockId uuid NULL,
    LockedUntilUtc timestamptz NULL
);

CREATE INDEX IF NOT EXISTS IX_InventoryOutboxMessages_Pending
ON InventoryOutboxMessages (NextAttemptAtUtc, OccurredAtUtc)
WHERE ProcessedAtUtc IS NULL;
