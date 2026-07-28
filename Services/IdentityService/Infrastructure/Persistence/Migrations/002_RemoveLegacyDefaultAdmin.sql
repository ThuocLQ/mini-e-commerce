-- Removes the deterministic administrator created by the original learning migration.
-- New environments never receive an account from migrations; local development can
-- opt into an explicitly configured bootstrap account instead.
DELETE FROM Users
WHERE Id = '11111111-1111-1111-1111-111111111111'
  AND UserName = 'admin'
  AND NormalizedUserName = 'ADMIN';
