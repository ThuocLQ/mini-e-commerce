CREATE TABLE IF NOT EXISTS CustomerAddresses (
    Id uuid PRIMARY KEY,
    CustomerId uuid NOT NULL REFERENCES Users(Id),
    Label text NOT NULL,
    RecipientName text NOT NULL,
    Line1 text NOT NULL,
    Line2 text NULL,
    City text NOT NULL,
    CountryCode varchar(2) NOT NULL,
    PostalCode text NULL,
    IsDefault boolean NOT NULL DEFAULT false,
    IsArchived boolean NOT NULL DEFAULT false,
    CreatedAtUtc timestamptz NOT NULL,
    UpdatedAtUtc timestamptz NOT NULL,
    CreateIdempotencyKey text NULL,
    CreateRequestHash varchar(64) NULL,
    CHECK (NOT (IsDefault AND IsArchived))
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_CustomerAddresses_OneDefault ON CustomerAddresses (CustomerId) WHERE IsDefault AND NOT IsArchived;
CREATE UNIQUE INDEX IF NOT EXISTS IX_CustomerAddresses_CreateIdempotency ON CustomerAddresses (CustomerId, CreateIdempotencyKey) WHERE CreateIdempotencyKey IS NOT NULL;
