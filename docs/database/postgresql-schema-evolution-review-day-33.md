# Day 33 PostgreSQL Schema Evolution Review

## Current Data Stores

| Service | Store | Ownership | Migration/initializer |
| --- | --- | --- | --- |
| CatalogService | PostgreSQL | Products | DbUp SQL migrations |
| OrderingService | PostgreSQL | Orders, order items, outbox | DbUp SQL migrations |
| DiscountService | PostgreSQL | Coupons | DbUp SQL migrations |
| IdentityService | PostgreSQL | Users | DbUp SQL migrations |
| PaymentService | PostgreSQL | Payments | DbUp SQL migrations |
| BasketService | Redis | Shopping carts | Runtime cache/state |
| OrderQueryService | MongoDB | Order summaries read model | Mongo initializer |

## Migration Files

Current relational migration folders:

```text
Services/CatalogService/Infrastructure/Persistence/Migrations
Services/DiscountService/Infrastructure/Persistence/Migrations
Services/IdentityService/Infrastructure/Persistence/Migrations
Services/OrderingService/Infrastructure/Persistence/Migrations
Services/PaymentService/Infrastructure/Persistence/Migrations
```

## SQLite Leftovers

SQLite references should be classified before action:

```text
Production code/config: should be removed or migrated.
Historical lessons: can remain as learning history.
Backlog/handoff docs: update when they mislead current work.
```

Current Day 33 scope is review and policy. It is not a broad rewrite.

## Risks

```text
Startup migrations can hide production rollout risk if logs are ignored.
Seed data policy is not fully standardized.
Backup and restore drills are not implemented.
Connection string secrets are still local-development oriented.
Index coverage needs deeper query-path review.
```

## Future Work

```text
Add migration smoke tests.
Add backup/restore runbook.
Add Testcontainers integration tests.
Review indexes per service.
Document seed data lifecycle.
```
