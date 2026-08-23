ALTER TABLE Orders
ADD COLUMN IF NOT EXISTS CheckoutRequestHash text NULL;

ALTER TABLE Orders
ADD COLUMN IF NOT EXISTS CheckoutBasketVersion bigint NULL;

ALTER TABLE Orders
ADD COLUMN IF NOT EXISTS CheckoutBasketId uuid NULL;

CREATE INDEX IF NOT EXISTS IX_Orders_CustomerId_IdempotencyKey_CheckoutRequestHash
ON Orders(CustomerId, IdempotencyKey, CheckoutRequestHash)
WHERE IdempotencyKey IS NOT NULL;
