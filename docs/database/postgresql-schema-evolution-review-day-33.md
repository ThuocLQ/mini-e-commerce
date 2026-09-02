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

## P1 Review Outcome

- DbUp migrations are forward-only and versioned for Catalog, Inventory, Ordering, Discount, Identity, Payment and Notification. SupplierService uses Flyway migrations; Basket remains Redis and OrderQuery remains MongoDB.
- Query-path indexes cover checkout idempotency, order/customer lookup, payment/webhook deduplication, reservation expiry, outbox dispatch, shipment timeline, notification delivery lease and procurement receipt/audit paths. New query endpoints require an index review before release.
- Npgsql uses its provider-managed connection pool. Connection strings are service-local and private; outbound HTTP clients have explicit bounded timeouts plus standard resilience. Any production pool-size override must be capacity-tested and supplied by environment, not hard-coded.
- `scripts/portfolio-seed.ps1` imports the versioned catalog CSV idempotently through the admin API. It is demo data only, never production bootstrap data. Production seed data must be migration-owned reference data or an audited import.
- Rebuild mode runs ProjectionWorker with a dedicated consumer group and writes to `order_summaries_rebuild`; it never overwrites the live collection. Promotion/swap and replay remain a runbook operation.
