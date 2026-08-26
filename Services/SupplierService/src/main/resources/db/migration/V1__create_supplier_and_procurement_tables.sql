CREATE TABLE suppliers (
    id uuid PRIMARY KEY,
    name varchar(160) NOT NULL,
    contact_email varchar(320),
    active boolean NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz NOT NULL
);

CREATE TABLE purchase_orders (
    id uuid PRIMARY KEY,
    purchase_order_number varchar(48) NOT NULL UNIQUE,
    supplier_id uuid NOT NULL REFERENCES suppliers(id),
    status varchar(32) NOT NULL,
    currency varchar(3) NOT NULL,
    created_at_utc timestamptz NOT NULL,
    submitted_at_utc timestamptz NULL,
    CONSTRAINT ck_purchase_orders_status CHECK (status IN ('DRAFT', 'SUBMITTED', 'CANCELLED'))
);

CREATE INDEX ix_purchase_orders_supplier_created_at ON purchase_orders (supplier_id, created_at_utc DESC);

CREATE TABLE purchase_order_lines (
    id uuid PRIMARY KEY,
    purchase_order_id uuid NOT NULL REFERENCES purchase_orders(id) ON DELETE CASCADE,
    product_id varchar(128) NOT NULL,
    product_name varchar(200) NOT NULL,
    quantity integer NOT NULL CHECK (quantity > 0),
    unit_cost numeric(18,2) NOT NULL CHECK (unit_cost >= 0),
    CONSTRAINT uq_purchase_order_line_product UNIQUE (purchase_order_id, product_id)
);