---
day: 33
title: "PostgreSQL + Migration + Schema Evolution Hardening"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
encoding_note: "UTF-8 Markdown tiếng Việt chuẩn"
style: "learning-practice"
---

# Day 33: PostgreSQL + Migration + Schema Evolution Hardening

## 0. Hôm nay học gì?

Hôm nay mình học cách review database ownership, migration và schema evolution.

Bài này không phải chuyển SQLite sang PostgreSQL mù quáng. Repo hiện đã dùng PostgreSQL ở các write-side service cần thiết.

## 1. Vì sao cần bài này?

Database change là phần rất dễ làm hỏng production. Một column đổi sai có thể làm service crash, data mất, hoặc deploy rollback khó.

Vì vậy backend engineer cần biết cách thay schema an toàn.

## 2. Khái niệm cốt lõi

### Database ownership

Mỗi service nên sở hữu data của nó.

```text
OrderingService sở hữu order/outbox data.
CatalogService sở hữu product data.
BasketService sở hữu basket state/cache.
OrderQueryService sở hữu read model.
```

### Migration

Migration là cách thay đổi schema có dấu vết, review được, chạy lại được.

### Schema evolution

Schema evolution là cách thay đổi DB từ từ:

```text
Add nullable column.
Backfill.
Deploy code mới.
Enforce constraint sau.
```

## 3. Nhìn vào repo hiện tại

Các service chính:

```text
Services/ApiGateway
Services/CatalogService
Services/BasketService
Services/OrderingService
Services/DiscountService
Services/IdentityService
Services/PaymentService
Services/OrderQueryService
```

Các worker:

```text
Services/NotificationWorker
Workers/ProjectionWorker
```

Các project dùng chung:

```text
BuildingBlocks.Contracts
MicroShop.AppHost
MicroShop.ServiceDefaults
```

Hạ tầng local:

```text
PostgreSQL: lưu dữ liệu write-side
Redis: lưu basket/cache
RabbitMQ: workflow/task messaging
Kafka: event stream/projection learning
MongoDB: read model và projection failure
Docker Compose: chạy local runtime
Aspire AppHost: orchestration local .NET
```

Route/ID cũ không dùng lại:

```text
/orders/read-model
ORD-900
CUST-900
```


Cần verify store thật theo code, không tự đoán.

SQLite nếu nằm trong docs cũ thì chỉ là historical material. Nếu nằm trong production code/config hiện tại mới là issue.

## 4. Thực hành từng bước

### Bước 1: Search database config

```powershell
Get-ChildItem Services -Recurse -Filter appsettings*.json |
  Select-String -Pattern "ConnectionStrings|Postgres|PostgreSQL|Npgsql|Sqlite|SQLite|Mongo|Redis"
```

### Bước 2: Search database code

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "Npgsql|Dapper|DbConnection|ConnectionFactory|DatabaseInitializer|Migration|Sqlite|SQLite"
```

### Bước 3: Inspect migrations

```powershell
Get-ChildItem Services -Recurse -Directory -Filter Migrations
Get-ChildItem Services -Recurse -Filter *DatabaseInitializer*.cs
Get-ChildItem Services -Recurse -Filter *ConnectionFactory*.cs
```

### Bước 4: Tạo database review

Tạo:

```text
docs/database/postgresql-schema-evolution-review-day-33.md
```

Bảng:

```text
Service | Primary store | Data sở hữu | Migration/initializer | Risk
```

### Bước 5: Tạo migration policy

Tạo:

```text
docs/database/migration-policy.md
```

Ghi rule:

```text
Không thay schema âm thầm.
Ưu tiên additive change.
Không drop/rename/type change nếu chưa có plan.
Rollback phải được nghĩ trước.
```

## 5. Kết quả kỳ vọng

Kỳ vọng:

```text
Biết service nào dùng DB nào.
Biết SQLite hit nào là historical docs, hit nào là code thật.
Có migration policy.
Có backlog DB hardening.
```

## 6. Lỗi hay gặp

```text
Lỗi 1: Thấy chữ SQLite trong docs cũ rồi kết luận code production sai.
Lỗi 2: Xóa migration cũ.
Lỗi 3: Đổi DB/schema trong lúc bài chỉ yêu cầu review.
Lỗi 4: Không phân biệt write-side DB và MongoDB read model.
```

## 7. Tổng kết bài học

Day 33 giúp mình học tư duy database lifecycle: không chỉ CRUD được là xong, mà còn phải biết schema thay đổi thế nào cho an toàn.

## 8. Checklist trước khi commit

```text
[ ] Hiểu được mục tiêu chính của bài.
[ ] Đã chạy các lệnh kiểm tra chính.
[ ] Đã tạo/cập nhật đúng docs hoặc code trong scope.
[ ] Đã test phần cần test.
[ ] Không dùng endpoint/id cũ.
[ ] Không claim production-ready.
[ ] Không làm lệch RabbitMQ/Kafka responsibility.
```

## 9. Commit/tag gợi ý

```text
Commit: Day 33: PostgreSQL Migration Schema Review
Tag: day-33-postgresql-migration-schema-review
```
