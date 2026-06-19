CREATE TABLE IF NOT EXISTS PaymentOutboxMessages (
    Id uuid PRIMARY KEY,
    OccurredAtUtc timestamptz NOT NULL,
    Type text NOT NULL,
    Content text NOT NULL,
    Status text NOT NULL DEFAULT 'Pending',
    RetryCount integer NOT NULL DEFAULT 0,
    Error text NULL,
    NextAttemptAtUtc timestamptz NOT NULL,
    ProcessedAtUtc timestamptz NULL,
    LockedBy uuid NULL,
    LockedUntilUtc timestamptz NULL
);

CREATE INDEX IF NOT EXISTS IX_PaymentOutboxMessages_Status_NextAttemptAtUtc
ON PaymentOutboxMessages(Status, NextAttemptAtUtc);
