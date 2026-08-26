ALTER TABLE purchase_orders
    ADD COLUMN receipt_id uuid NULL,
    ADD COLUMN receipt_requested_at_utc timestamptz NULL,
    ADD COLUMN received_at_utc timestamptz NULL;

ALTER TABLE purchase_orders DROP CONSTRAINT ck_purchase_orders_status;
ALTER TABLE purchase_orders
    ADD CONSTRAINT ck_purchase_orders_status
        CHECK (status IN ('DRAFT', 'SUBMITTED', 'RECEIPT_PENDING', 'RECEIVED', 'CANCELLED'));

CREATE UNIQUE INDEX ux_purchase_orders_receipt_id
    ON purchase_orders (receipt_id)
    WHERE receipt_id IS NOT NULL;
