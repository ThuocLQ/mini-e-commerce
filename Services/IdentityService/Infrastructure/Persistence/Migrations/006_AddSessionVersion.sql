ALTER TABLE Users
    ADD COLUMN IF NOT EXISTS SessionVersion integer NOT NULL DEFAULT 1;

ALTER TABLE Users
    ADD CONSTRAINT ck_users_session_version_positive
    CHECK (SessionVersion > 0);