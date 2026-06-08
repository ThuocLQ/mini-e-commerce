---
day: 33
title: "PostgreSQL + Migration + Schema Evolution Hardening"
duration: "90-120 minutes"
phase: "Stage 2 - Production Hardening"
project: "MicroShop"
testing: "Database inspection + migration review + smoke checks"
type: "lesson"
repo_aware: true
source_of_truth: true
encoding_note: "ASCII-safe Markdown to avoid mojibake in Notion/Rider/GitHub"
---

# Day 33: PostgreSQL + Migration + Schema Evolution Hardening

## 0. Current position

You completed Day 31 architecture review and Day 32 API contract hardening.

Day 33 focuses on database hardening.

Important correction:

```text
The repo already uses PostgreSQL for write-side relational databases where applicable.
Day 33 is not a naive SQLite -> PostgreSQL rewrite unless the repo still has leftover SQLite usage.
Day 33 reviews PostgreSQL usage, migrations, schema evolution, and connection/config safety.
```

## 1. Current repo context

Current repo truth:

```text
Services:
- Services/ApiGateway
- Services/CatalogService
- Services/BasketService
- Services/OrderingService
- Services/DiscountService
- Services/IdentityService
- Services/PaymentService
- Services/OrderQueryService

Background workers:
- Services/NotificationWorker
- Workers/ProjectionWorker

Shared:
- BuildingBlocks.Contracts
- MicroShop.AppHost
- MicroShop.ServiceDefaults
```

Important routes:

```text
GET /order-summaries
GET /order-summaries/{orderId}
POST /debug/order-summaries

GET /orders
GET /orders/{id}
POST /orders/checkout
GET /debug/outbox

GET /products
GET /products/{id}
GET /products/search
GET /products/count
GET /products/price-range
POST /products
PUT /products/{id}
DELETE /products/{id}

GET /basket/{userId}
POST /basket/{userId}/items
POST /basket/{userId}/items-grpc
PUT /basket/{userId}/items/{productId}
DELETE /basket/{userId}/items/{productId}
PUT /basket/{userId}/clear
DELETE /basket/{userId}
GET /basket/products/{productId}/validate
GET /basket/products/{productId}/validate-grpc
POST /basket/preview-item
POST /basket/preview-item-grpc
GET /basket/products/{productId}/compare-communication

GET /discounts/{code}
POST /discounts/apply

POST /auth/login
GET /auth/me

POST /payments
GET /payments/{id}
POST /webhooks/payment
POST /payments/webhooks/payment

GET /health
GET /alive
```

Never use:

```text
/orders/read-model
ORD-900
CUST-900
```


Current infrastructure:

```text
PostgreSQL - write-side relational databases
Redis - BasketService state/cache
MongoDB - read model and projection failures
Kafka - event stream/projection learning
RabbitMQ - workflow/task messaging
```

## 2. Goal

```text
[ ] Current database usage is documented per service.
[ ] PostgreSQL configuration is reviewed.
[ ] Any leftover SQLite references are found and classified.
[ ] Migration strategy is documented.
[ ] Schema evolution rules are documented.
[ ] Connection string/config safety is reviewed.
[ ] Basic DB smoke checks are documented.
```

Main outputs:

```text
docs/database/postgresql-schema-evolution-review-day-33.md
docs/database/migration-policy.md
docs/backlog/day-33-database-hardening-backlog.md
```

## 3. Scope guard

Do:

```text
[ ] Inspect database configuration.
[ ] Inspect migrations/initializers.
[ ] Document current DB ownership.
[ ] Define migration/schema evolution policy.
[ ] Create backlog.
```

Do not:

```text
[ ] Do not rewrite all persistence code.
[ ] Do not switch databases blindly.
[ ] Do not delete old migrations.
[ ] Do not change Kafka/MongoDB projection behavior.
[ ] Do not claim database strategy is production-complete.
```

What this proves:

```text
MicroShop has a reviewed database baseline and schema evolution policy.
```

What this does not prove:

```text
Zero-downtime migration is implemented.
Backup/restore is production-ready.
```

## 4. Pre-check

```powershell
git status --short
docker compose config --services
Get-ChildItem Services -Recurse -Filter appsettings*.json |
  Select-String -Pattern "ConnectionStrings|Postgres|PostgreSQL|Npgsql|Sqlite|SQLite|Mongo|Redis"
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "Npgsql|Dapper|DbConnection|ConnectionFactory|DatabaseInitializer|Migration|Sqlite|SQLite"
Get-ChildItem Services -Recurse -Directory -Filter Migrations
Get-ChildItem Services -Recurse -Filter *DatabaseInitializer*.cs
Get-ChildItem Services -Recurse -Filter *ConnectionFactory*.cs
```

## 5. Database ownership review

Create table:

```text
Service | Primary data store | Owns which data | Migration/initializer strategy | Risks
```

Verify before filling.

Suggested starting point:

```text
CatalogService -> PostgreSQL/write-side product data
OrderingService -> PostgreSQL/write-side order/outbox data
BasketService -> Redis basket state/cache
OrderQueryService -> MongoDB read model only
```

Do not invent unverified details.

## 6. Create migration policy

Create:

```text
docs/database/migration-policy.md
```

Include:

```text
Do not change schema silently.
Prefer migration scripts or migration tools.
Keep service-owned schemas separate.
Document breaking schema changes.
Use additive changes first.
Avoid destructive migrations without compatibility window.
```

## 7. Create database review doc

Create:

```text
docs/database/postgresql-schema-evolution-review-day-33.md
```

Include:

```text
Current databases table
Connection string findings
Migration/initializer findings
SQLite leftovers if any
Schema evolution risks
What this proves / what this does not prove
```

## 8. Runtime smoke checks

Full system if machine can handle it:

```powershell
docker compose up -d --build
```

Check:

```powershell
docker compose ps postgres
docker compose logs postgres --tail 100
docker compose logs catalogservice --tail 100
docker compose logs orderingservice --tail 100
```

Use actual service names from compose.

Do not invent credentials.

## 9. Backlog

Create:

```text
docs/backlog/day-33-database-hardening-backlog.md
```

Include:

```text
[ ] Standardize migration approach per relational service.
[ ] Document database ownership per service.
[ ] Remove or archive stale SQLite references if any.
[ ] Review connection string handling and secrets.
[ ] Add DB + one service smoke tests.
[ ] Add migration smoke tests.
[ ] Add seed data policy.
[ ] Add backup/restore drill.
[ ] Review indexes for main query paths.
[ ] Add Testcontainers integration tests later.
```

## 10. Docs/Postman updates

Update:

```text
docs/README.md
```

Add links to database docs.

Postman not required unless endpoint behavior changed.

Optional smoke routes:

```text
GET /products
GET /orders
GET /order-summaries
```

Only use routes that are running and configured.

## 11. Build/test plan

```powershell
dotnet build Services/CatalogService/CatalogService.csproj
dotnet build Services/OrderingService/OrderingService.csproj
dotnet build Services/IdentityService/IdentityService.csproj
dotnet build Services/PaymentService/PaymentService.csproj
dotnet build Services/DiscountService/DiscountService.csproj
```

If reasonable:

```powershell
dotnet build
```

Document unrelated failures.

## 12. Production fit review

Improves:

```text
Database ownership clarity.
Migration risk visibility.
Schema evolution policy.
```

Future work:

```text
Automated migration pipeline.
Rollback drills.
Backup/restore.
Testcontainers.
Zero-downtime migration techniques.
```

## 13. Pass checklist

```text
[ ] Current DB usage is inspected.
[ ] PostgreSQL config is reviewed.
[ ] SQLite leftovers are searched and classified as production code/config/docs/history.
[ ] Migration/initializer files are inspected.
[ ] migration-policy.md exists.
[ ] postgresql-schema-evolution-review-day-33.md exists.
[ ] day-33 database backlog exists.
[ ] docs/README.md links new database docs.
[ ] Build passes for reviewed/touched projects or failures are documented.
[ ] No broad persistence rewrite is introduced.
```

## 14. Optional commit/tag after review

```text
Commit: Day 33: PostgreSQL Migration Schema Review
Tag: day-33-postgresql-migration-schema-review
```

## 15. Next day

```text
Day 34: FluentValidation Pipeline + Mapping
```
