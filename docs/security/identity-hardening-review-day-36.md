# Day 36 Identity Hardening Review

## Current Routes

```text
POST /auth/login
GET /auth/me
```

## Current Implementation Notes

```text
IdentityService uses local JWT authentication for learning.
/auth/me requires authorization.
JwtOptions contains Issuer, Audience, SecretKey, and ExpirationMinutes.
Password hashing is abstracted behind IPasswordHasher.
User persistence is abstracted behind IUserRepository.
```

## Review Checklist

```text
[ ] Password hashing remains server-side and no raw password is logged.
[ ] JWT signing key is configuration-driven and not documented as a production secret.
[ ] Token lifetime is documented and reviewed.
[ ] /auth/me requires a valid token.
[ ] Failed login behavior is documented.
[ ] Claims stay minimal and understandable.
```

## Not Implemented Yet

```text
Refresh tokens.
RBAC/permissions beyond basic role claims.
External OIDC/SSO.
Account lockout.
Audit log implementation.
Secret rotation.
```

## Production Direction

Keep the local JWT foundation for learning, then move authentication to an external Identity Provider when the project reaches a deeper production-hardening stage.
