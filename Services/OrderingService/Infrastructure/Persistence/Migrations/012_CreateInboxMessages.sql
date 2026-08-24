CREATE TABLE IF NOT EXISTS InboxMessages (
    EventId uuid NOT NULL,
    ConsumerName text NOT NULL,
    ReceivedAtUtc timestamptz NOT NULL,
    CONSTRAINT PK_InboxMessages PRIMARY KEY (ConsumerName, EventId)
);

CREATE INDEX IF NOT EXISTS IX_InboxMessages_ReceivedAtUtc
ON InboxMessages(ReceivedAtUtc);
