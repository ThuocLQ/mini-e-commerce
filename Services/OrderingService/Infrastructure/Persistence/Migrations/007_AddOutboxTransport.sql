ALTER TABLE OutboxMessages
    ADD COLUMN IF NOT EXISTS Transport text NOT NULL DEFAULT 'RabbitMq';

CREATE INDEX IF NOT EXISTS IX_OutboxMessages_PendingTransport
ON OutboxMessages(Transport, NextAttemptAtUtc, OccurredAtUtc)
WHERE ProcessedAtUtc IS NULL;
