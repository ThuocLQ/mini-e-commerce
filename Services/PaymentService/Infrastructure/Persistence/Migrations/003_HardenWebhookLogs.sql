ALTER TABLE WebhookLogs
ADD COLUMN IF NOT EXISTS PayloadHash text NULL;

ALTER TABLE WebhookLogs
ADD COLUMN IF NOT EXISTS SignatureStatus text NULL;

CREATE INDEX IF NOT EXISTS IX_WebhookLogs_SignatureStatus_ReceivedAtUtc
ON WebhookLogs(SignatureStatus, ReceivedAtUtc);
