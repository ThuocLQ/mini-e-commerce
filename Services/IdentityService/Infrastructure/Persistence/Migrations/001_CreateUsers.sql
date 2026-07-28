CREATE TABLE IF NOT EXISTS Users (
    Id uuid PRIMARY KEY,
    UserName text NOT NULL,
    NormalizedUserName text NOT NULL UNIQUE,
    PasswordHash text NOT NULL,
    Role text NOT NULL,
    IsActive boolean NOT NULL
);
