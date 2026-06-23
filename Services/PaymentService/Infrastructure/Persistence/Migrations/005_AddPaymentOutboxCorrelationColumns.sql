ALTER TABLE PaymentOutboxMessages
ADD COLUMN IF NOT EXISTS CorrelationId text NULL;

ALTER TABLE PaymentOutboxMessages
ADD COLUMN IF NOT EXISTS CausationId text NULL;

CREATE INDEX IF NOT EXISTS IX_PaymentOutboxMessages_CorrelationId
ON PaymentOutboxMessages(CorrelationId)
WHERE CorrelationId IS NOT NULL;
