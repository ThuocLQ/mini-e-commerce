# Day 36 Audit Identity Hardening Backlog

## Audit

```text
[ ] Define audit log storage.
[ ] Implement append-only audit writer.
[ ] Audit login success and failure.
[ ] Audit payment webhook received.
[ ] Audit order checkout.
[ ] Add correlationId/traceId to audit entries.
```

## Identity

```text
[ ] Review JWT signing key storage.
[ ] Add token lifetime policy documentation.
[ ] Consider refresh tokens.
[ ] Consider RBAC/permissions.
[ ] Consider account lockout after repeated failures.
[ ] Consider external OIDC/SSO.
```

## Strategy Pattern

```text
[ ] Review existing DiscountService strategy test coverage.
[ ] Document how to add a new discount strategy.
[ ] Consider PaymentService provider strategy later.
[ ] Consider auth provider strategy later.
```
