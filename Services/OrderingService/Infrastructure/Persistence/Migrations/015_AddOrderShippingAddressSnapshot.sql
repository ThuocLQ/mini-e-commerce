ALTER TABLE Orders ADD COLUMN IF NOT EXISTS ShippingAddressId uuid NULL;
ALTER TABLE Orders ADD COLUMN IF NOT EXISTS ShippingAddressLabel varchar(100) NULL;
ALTER TABLE Orders ADD COLUMN IF NOT EXISTS ShippingRecipientName varchar(200) NULL;
ALTER TABLE Orders ADD COLUMN IF NOT EXISTS ShippingLine1 varchar(300) NULL;
ALTER TABLE Orders ADD COLUMN IF NOT EXISTS ShippingLine2 varchar(300) NULL;
ALTER TABLE Orders ADD COLUMN IF NOT EXISTS ShippingCity varchar(100) NULL;
ALTER TABLE Orders ADD COLUMN IF NOT EXISTS ShippingCountryCode varchar(2) NULL;
ALTER TABLE Orders ADD COLUMN IF NOT EXISTS ShippingPostalCode varchar(32) NULL;

ALTER TABLE Orders
    ADD CONSTRAINT CK_Orders_ShippingAddressSnapshot_Complete
    CHECK (
        (ShippingAddressId IS NULL AND ShippingAddressLabel IS NULL AND ShippingRecipientName IS NULL AND ShippingLine1 IS NULL AND ShippingLine2 IS NULL AND ShippingCity IS NULL AND ShippingCountryCode IS NULL AND ShippingPostalCode IS NULL)
        OR
        (ShippingAddressId IS NOT NULL AND ShippingAddressLabel IS NOT NULL AND ShippingRecipientName IS NOT NULL AND ShippingLine1 IS NOT NULL AND ShippingCity IS NOT NULL AND ShippingCountryCode IS NOT NULL)
    );
