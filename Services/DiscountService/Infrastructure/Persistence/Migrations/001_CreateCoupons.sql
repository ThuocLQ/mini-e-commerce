CREATE TABLE IF NOT EXISTS Coupons (
    Code text PRIMARY KEY,
    Type text NOT NULL,
    Value numeric(18, 2) NOT NULL,
    ValidFromUtc timestamptz NOT NULL,
    ValidToUtc timestamptz NOT NULL,
    IsActive boolean NOT NULL
);

INSERT INTO Coupons (Code, Type, Value, ValidFromUtc, ValidToUtc, IsActive)
VALUES
    ('SAVE10', 'Percentage', 10, '2024-01-01T00:00:00Z', '2035-12-31T23:59:59Z', true),
    ('WELCOME50', 'FixedAmount', 50, '2024-01-01T00:00:00Z', '2035-12-31T23:59:59Z', true),
    ('EXPIRED20', 'Percentage', 20, '2024-01-01T00:00:00Z', '2024-12-31T23:59:59Z', true),
    ('DISABLED15', 'Percentage', 15, '2024-01-01T00:00:00Z', '2035-12-31T23:59:59Z', false)
ON CONFLICT (Code) DO NOTHING;
