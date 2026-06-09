 Day 33: PostgreSQL + Migration + Schema Evolution Hardening

## 0. Vị trí hiện tại

Bạn đã hoàn thành Day 31 architecture review va Day 32 API contract hardening.

Day 33 focuses on database hardening.

Important correction:

```text
The repo already uses PostgreSQL for write-side relational databases where applicable.
Day 33 is not a naive SQLite -> PostgreSQL rewrite unless the repo still has leftover SQLite usage.
Day 33 reviews PostgreSQL usage, migrations, schema evolution, and connection/config safety.
```

## 1. Bối cảnh repo hiện tại

Sự thật hiện tại của repo:

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

Các route quan trọng:

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

Không dùng:

```text
/orders/read-model
ORD-900
CUST-900
```


Hạ tầng hiện tại:

```text
PostgreSQL - write-side relational databases
Redis - BasketService state/cache
MongoDB - read model and projection failures
Kafka - event stream/projection learning
RabbitMQ - workflow/task messaging
```

## 2. Mục tiêu

```text
[ ] Current database usage is documented per service.
[ ] PostgreSQL configuration is reviewed.
[ ] Any leftover SQLite references are found and classified.
[ ] Migration strategy is documented.
[ ] Schema evolution rules are documented.
[ ] Connection string/config safety is reviewed.
[ ] Basic DB smoke checks are documented.
```

Output chính:

```text
docs/database/postgresql-schema-evolution-review-day-33.md
docs/database/migration-policy.md
docs/backlog/day-33-database-hardening-backlog.md
```

## 3. Giới hạn phạm vi

Nên làm:

```text
[ ] Inspect database configuration.
[ ] Inspect migrations/initializers.
[ ] Document current DB ownership.
[ ] Define migration/schema evolution policy.
[ ] Create backlog.
```

Không làm:

```text
[ ] Do not rewrite all persistence code.
[ ] Do not switch databases blindly.
[ ] Do not delete old migrations.
[ ] Do not change Kafka/MongoDB projection behavior.
[ ] Do not claim database strategy is production-complete.
```

Điều phần này chứng minh:

```text
MicroShop has a reviewed database baseline and schema evolution policy.
```

Điều phần này chưa chứng minh:

```text
Zero-downtime migration is implemented.
Backup/restore is production-ready.
```

## 4. Kiểm tra trước khi làm

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

Tạo:

```text
docs/database/migration-policy.md
```

Bao gồm:

```text
Do not change schema silently.
Prefer migration scripts or migration tools.
Keep service-owned schemas separate.
Document breaking schema changes.
Use additive changes first.
Avoid destructive migrations without compatibility window.
```

## 7. Create database review doc

Tạo:

```text
docs/database/postgresql-schema-evolution-review-day-33.md
```

Bao gồm:

```text
Current databases table
Connection string findings
Migration/initializer findings
SQLite leftovers if any
Schema evolution risks
Điều phần này chứng minh / điều phần này chưa chứng minh
```

## 8. Smoke test runtime

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

Không tự bịa credentials.

## 9. Backlog

Tạo:

```text
docs/backlog/day-33-database-hardening-backlog.md
```

Bao gồm:

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

Route smoke test tùy chọn:

```text
GET /products
GET /orders
GET /order-summaries
```

Chỉ dùng route đang chạy và đã được cấu hình.

## 11. Kế hoạch build/test

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

Ghi lại các lỗi không liên quan nếu có.

## 12. Review độ phù hợp production-minded

Improves:

```text
Database ownership clarity.
Migration risk visibility.
Schema evolution policy.
```

Việc làm sau:

```text
Automated migration pipeline.
Rollback drills.
Backup/restore.
Testcontainers.
Zero-downtime migration techniques.
```

## 13. Checklist đạt yêu cầu

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
[ ] Không đưa vào refactor persistence diện rộng.
```

## 14. Commit/tag tùy chọn sau review

```text
Commit: Day 33: PostgreSQL Migration Schema Review
Tag: day-33-postgresql-migration-schema-review
```

## 15. Ngày tiếp theo

```text
Day 34: FluentValidation Pipeline + Mapping
```

