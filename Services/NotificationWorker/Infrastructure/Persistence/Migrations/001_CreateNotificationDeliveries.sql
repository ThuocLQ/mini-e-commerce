CREATE TABLE IF NOT EXISTS NotificationDeliveries
(
    Id uuid PRIMARY KEY,
    EventId uuid NOT NULL,
    EventType varchar(200) NOT NULL,
    Template varchar(100) NOT NULL,
    Channel varchar(50) NOT NULL,
    CustomerId uuid NOT NULL,
    OrderId uuid NULL,
    CorrelationId varchar(128) NULL,
    Status varchar(32) NOT NULL,
    AttemptCount integer NOT NULL DEFAULT 0,
    ProcessingLeaseToken uuid NULL,
    ProcessingLeaseExpiresAtUtc timestamptz NULL,
    LastError text NULL,
    CreatedAtUtc timestamptz NOT NULL,
    UpdatedAtUtc timestamptz NOT NULL,
    SentAtUtc timestamptz NULL,
    CONSTRAINT UQ_NotificationDeliveries_Event_Template_Channel UNIQUE (EventId, Template, Channel)
);

CREATE INDEX IF NOT EXISTS IX_NotificationDeliveries_Status_Lease
    ON NotificationDeliveries (Status, ProcessingLeaseExpiresAtUtc);

CREATE INDEX IF NOT EXISTS IX_NotificationDeliveries_CustomerId_CreatedAtUtc
    ON NotificationDeliveries (CustomerId, CreatedAtUtc DESC);

CREATE TABLE IF NOT EXISTS NotificationDeliveryAttempts
(
    DeliveryId uuid NOT NULL REFERENCES NotificationDeliveries(Id) ON DELETE CASCADE,
    AttemptNumber integer NOT NULL,
    AttemptedAtUtc timestamptz NOT NULL,
    CompletedAtUtc timestamptz NULL,
    Outcome varchar(32) NOT NULL,
    Error text NULL,
    CONSTRAINT PK_NotificationDeliveryAttempts PRIMARY KEY (DeliveryId, AttemptNumber)
);