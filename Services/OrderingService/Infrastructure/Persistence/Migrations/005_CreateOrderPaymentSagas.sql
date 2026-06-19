CREATE TABLE IF NOT EXISTS OrderPaymentSagas (
    Id uuid PRIMARY KEY,
    OrderId uuid NOT NULL REFERENCES Orders(Id) ON DELETE CASCADE,
    PaymentId uuid NOT NULL,
    State text NOT NULL,
    StartedAtUtc timestamptz NOT NULL,
    UpdatedAtUtc timestamptz NOT NULL,
    TimeoutAtUtc timestamptz NOT NULL,
    LastProcessedEventId uuid NULL,
    LastError text NULL,
    CONSTRAINT UQ_OrderPaymentSagas_OrderId UNIQUE (OrderId)
);

CREATE INDEX IF NOT EXISTS IX_OrderPaymentSagas_PaymentId
ON OrderPaymentSagas(PaymentId);

CREATE INDEX IF NOT EXISTS IX_OrderPaymentSagas_State_TimeoutAtUtc
ON OrderPaymentSagas(State, TimeoutAtUtc);
