# MicroShop Migration Policy

## Goal

Schema changes should be explicit, reviewed, and owned by the service that owns the data.

## Ownership

Each service owns its own data model and migration path.

```text
CatalogService owns product write data.
OrderingService owns order and outbox write data.
DiscountService owns coupon write data.
IdentityService owns user write data.
PaymentService owns payment write data.
BasketService owns Redis basket state.
OrderQueryService owns MongoDB read models.
```

## Rules

```text
Do not change schema silently.
Prefer versioned migration scripts.
Keep service-owned schemas separate.
Use additive changes before destructive changes.
Document breaking schema changes.
Avoid deleting or rewriting old migrations in shared environments.
Do not rely on application startup magic without clear logs.
```

## Production Direction

```text
Migration execution should be observable.
Rollback strategy should be documented.
Backups should be tested before destructive changes.
Schema compatibility windows should be used for breaking changes.
```

## Current Local Approach

Relational write-side services use PostgreSQL with service-local migrations. Local Docker can run a single PostgreSQL instance with separate logical databases.
