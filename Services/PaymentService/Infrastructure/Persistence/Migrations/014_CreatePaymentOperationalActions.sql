CREATE TABLE IF NOT EXISTS PaymentOperationalActions
(
    Id uuid PRIMARY KEY,
    PaymentId uuid NOT NULL REFERENCES Payments(Id),
    ActionType text NOT NULL,
    RequestedBy text NOT NULL,
    Reason text NOT NULL,
    RequestedAtUtc timestamptz NOT NULL,
    CompletedAtUtc timestamptz NULL,
    FailureReason text NULL
);

CREATE INDEX IF NOT EXISTS IX_PaymentOperationalActions_PaymentId_RequestedAtUtc
    ON PaymentOperationalActions (PaymentId, RequestedAtUtc DESC);