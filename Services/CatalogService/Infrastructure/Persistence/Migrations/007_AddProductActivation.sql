ALTER TABLE Products
    ADD COLUMN IF NOT EXISTS IsActive boolean NOT NULL DEFAULT true;

CREATE INDEX IF NOT EXISTS IX_Products_Active_Name
ON Products (Name)
WHERE IsActive = true;
