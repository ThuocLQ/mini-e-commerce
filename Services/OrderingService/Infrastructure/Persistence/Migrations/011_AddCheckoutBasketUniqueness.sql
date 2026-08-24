CREATE UNIQUE INDEX IF NOT EXISTS IX_Orders_CustomerId_CheckoutBasket
ON Orders (CustomerId, CheckoutBasketId, CheckoutBasketVersion)
WHERE CheckoutBasketId IS NOT NULL AND CheckoutBasketVersion IS NOT NULL;
