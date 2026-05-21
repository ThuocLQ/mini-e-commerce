CREATE TABLE IF NOT EXISTS Products (
    Id text PRIMARY KEY,
    Name text NOT NULL,
    Description text NOT NULL DEFAULT '',
    Price numeric(18, 2) NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS IX_Products_Name
ON Products (Name);
