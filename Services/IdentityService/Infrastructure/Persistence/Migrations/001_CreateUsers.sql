CREATE TABLE IF NOT EXISTS Users (
    Id uuid PRIMARY KEY,
    UserName text NOT NULL,
    NormalizedUserName text NOT NULL UNIQUE,
    PasswordHash text NOT NULL,
    Role text NOT NULL,
    IsActive boolean NOT NULL
);

INSERT INTO Users (Id, UserName, NormalizedUserName, PasswordHash, Role, IsActive)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'admin',
    'ADMIN',
    'PBKDF2-SHA256.100000.AQIDBAUGBwgJCgsMDQ4PEA==.7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=',
    'Admin',
    true
)
ON CONFLICT (NormalizedUserName) DO NOTHING;
