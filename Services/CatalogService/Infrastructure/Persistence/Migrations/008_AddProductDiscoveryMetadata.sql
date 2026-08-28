ALTER TABLE Products
    ADD COLUMN IF NOT EXISTS Category text NULL;

ALTER TABLE Products
    ADD COLUMN IF NOT EXISTS ImageUrl text NULL;

CREATE INDEX IF NOT EXISTS IX_Products_Active_Category_Name
ON Products (Category, Name, Id)
WHERE IsActive = true;

CREATE INDEX IF NOT EXISTS IX_Products_Active_Price_Id
ON Products (Price, Id)
WHERE IsActive = true;