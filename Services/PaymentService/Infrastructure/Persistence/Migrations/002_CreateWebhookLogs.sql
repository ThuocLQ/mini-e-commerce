CREATE TABLE IF NOT EXISTS WebhookLogs (
    Id uuid PRIMARY KEY,
    ProviderEventId text NOT NULL,
    PaymentId uuid NOT NULL,
    ProviderTransactionId text NOT NULL,
    EventType text NOT NULL,
    Status text NOT NULL,
    Error text NULL,
    ReceivedAtUtc timestamptz NOT NULL,
    ProcessedAtUtc timestamptz NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_WebhookLogs_ProviderEventId
ON WebhookLogs(ProviderEventId);

CREATE INDEX IF NOT EXISTS IX_WebhookLogs_PaymentId
ON WebhookLogs(PaymentId);

CREATE INDEX IF NOT EXISTS IX_WebhookLogs_Status_ReceivedAtUtc
ON WebhookLogs(Status, ReceivedAtUtc);
