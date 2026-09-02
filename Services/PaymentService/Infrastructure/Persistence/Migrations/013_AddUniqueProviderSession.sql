CREATE UNIQUE INDEX IF NOT EXISTS UX_Payments_Provider_ProviderSessionId
    ON Payments (Provider, ProviderSessionId)
    WHERE Provider IS NOT NULL AND ProviderSessionId IS NOT NULL;