ALTER TABLE Orders
    ADD COLUMN IF NOT EXISTS DiscountReservationId uuid NULL;

CREATE UNIQUE INDEX IF NOT EXISTS IX_Orders_DiscountReservationId
    ON Orders (DiscountReservationId)
    WHERE DiscountReservationId IS NOT NULL;
