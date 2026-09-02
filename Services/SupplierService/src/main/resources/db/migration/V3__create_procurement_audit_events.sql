CREATE TABLE procurement_audit_events (
    id uuid PRIMARY KEY,
    supplier_id uuid NULL REFERENCES suppliers(id),
    purchase_order_id uuid NULL REFERENCES purchase_orders(id),
    receipt_id uuid NULL,
    action varchar(96) NOT NULL,
    actor varchar(200) NOT NULL,
    correlation_id varchar(128) NULL,
    occurred_at_utc timestamptz NOT NULL
);

CREATE INDEX ix_procurement_audit_events_purchase_order_occurred
    ON procurement_audit_events (purchase_order_id, occurred_at_utc DESC);

CREATE INDEX ix_procurement_audit_events_occurred
    ON procurement_audit_events (occurred_at_utc DESC);