CREATE TABLE IF NOT EXISTS PaymentInboxMessages (
    EventId uuid NOT NULL,
    ConsumerName text NOT NULL,
    ReceivedAtUtc timestamptz NOT NULL,
    CONSTRAINT PK_PaymentInboxMessages PRIMARY KEY (ConsumerName, EventId)
);

CREATE INDEX IF NOT EXISTS IX_PaymentInboxMessages_ReceivedAtUtc
ON PaymentInboxMessages(ReceivedAtUtc);
