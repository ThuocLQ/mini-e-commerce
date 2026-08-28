ALTER TABLE Users
    ADD COLUMN IF NOT EXISTS Email text NULL,
    ADD COLUMN IF NOT EXISTS NormalizedEmail text NULL;

CREATE UNIQUE INDEX IF NOT EXISTS UX_Users_NormalizedEmail
    ON Users (NormalizedEmail)
    WHERE NormalizedEmail IS NOT NULL;