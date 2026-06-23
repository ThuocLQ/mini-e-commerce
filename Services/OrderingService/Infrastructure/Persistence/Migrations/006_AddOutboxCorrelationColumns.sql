ALTER TABLE OutboxMessages
ADD COLUMN IF NOT EXISTS CorrelationId text NULL;

ALTER TABLE OutboxMessages
ADD COLUMN IF NOT EXISTS CausationId text NULL;

CREATE INDEX IF NOT EXISTS IX_OutboxMessages_CorrelationId
ON OutboxMessages(CorrelationId)
WHERE CorrelationId IS NOT NULL;
