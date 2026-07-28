CREATE TABLE IF NOT EXISTS WebhookEventConflicts (
    Id uuid PRIMARY KEY,
    ProviderEventId text NOT NULL,
    ExistingPaymentId uuid NOT NULL,
    IncomingPaymentId uuid NOT NULL,
    ExistingProviderTransactionId text NOT NULL,
    IncomingProviderTransactionId text NOT NULL,
    ExistingEventType text NOT NULL,
    IncomingEventType text NOT NULL,
    ExistingPayloadHash text NULL,
    IncomingPayloadHash text NULL,
    ExistingSignatureStatus text NULL,
    IncomingSignatureStatus text NULL,
    DetectedAtUtc timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_WebhookEventConflicts_ProviderEventId_DetectedAtUtc
ON WebhookEventConflicts(ProviderEventId, DetectedAtUtc DESC);
