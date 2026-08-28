ALTER TABLE Users
    ADD COLUMN IF NOT EXISTS IsEmailVerified boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS EmailVerifiedAtUtc timestamptz NULL;

CREATE TABLE IF NOT EXISTS EmailVerificationTokens (
    Id uuid PRIMARY KEY,
    UserId uuid NOT NULL REFERENCES Users (Id) ON DELETE CASCADE,
    TokenHash bytea NOT NULL UNIQUE,
    CreatedAtUtc timestamptz NOT NULL,
    ExpiresAtUtc timestamptz NOT NULL,
    ConsumedAtUtc timestamptz NULL
);

CREATE INDEX IF NOT EXISTS IX_EmailVerificationTokens_Active
    ON EmailVerificationTokens (UserId, ExpiresAtUtc)
    WHERE ConsumedAtUtc IS NULL;

CREATE TABLE IF NOT EXISTS IdentityOutboxMessages (
    Id uuid PRIMARY KEY,
    OccurredAtUtc timestamptz NOT NULL,
    Type text NOT NULL,
    Content jsonb NOT NULL,
    CorrelationId text NULL,
    NextAttemptAtUtc timestamptz NOT NULL,
    ProcessedAtUtc timestamptz NULL,
    RetryCount integer NOT NULL DEFAULT 0,
    LastError text NULL,
    LockId uuid NULL,
    LockedUntilUtc timestamptz NULL
);

CREATE INDEX IF NOT EXISTS IX_IdentityOutboxMessages_Pending
    ON IdentityOutboxMessages (NextAttemptAtUtc, OccurredAtUtc)
    WHERE ProcessedAtUtc IS NULL;