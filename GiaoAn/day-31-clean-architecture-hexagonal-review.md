---
day: 31
title: "Clean Architecture + Hexagonal Review"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
encoding_note: "UTF-8 Markdown tiếng Việt chuẩn"
style: "learning-practice"
---

# Day 31: Clean Architecture + Hexagonal Review

## 0. Hôm nay học gì?

Hôm nay mình học cách nhìn lại kiến trúc service: code nào nên nằm ở API, Application, Domain, Infrastructure.

Bài này là review kiến trúc, không phải refactor lớn.

## 1. Vì sao cần bài này?

Microservices dễ biến thành mớ code rối nếu boundary không rõ. Clean Architecture và Hexagonal giúp mình biết code đang phụ thuộc đúng chiều hay sai chiều.

Đi phỏng vấn Middle/Senior, biết nhìn boundary như này rất quan trọng.

## 2. Khái niệm cốt lõi

### Clean Architecture

Quy tắc đơn giản:

```text
Layer ngoài phụ thuộc layer trong.
Layer trong không phụ thuộc ngược ra layer ngoài.
```

Trong MicroShop:

```text
API: endpoint, request/response, HTTP concern
Application: use case, command/query/handler, interface/port
Domain: entity, value object, business rule
Infrastructure: DB, Redis, Kafka, RabbitMQ, MongoDB, external client
```

### Hexagonal Architecture

Hexagonal nói về ports/adapters:

```text
IOrderRepository = port
DapperOrderRepository = adapter

IOutboxRepository = port
DapperOutboxRepository = adapter
```

Use case không nên biết quá sâu về DB/broker/framework.

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


Các service cần review kỹ:

```text
OrderingService: checkout, order write, outbox
OrderQueryService: MongoDB read model query
ProjectionWorker: Kafka consumer + MongoDB projection
BasketService: Redis + REST/gRPC product validation
CatalogService: query/filter/product APIs
```

## 4. Thực hành từng bước

### Bước 1: Inspect folder structure

```powershell
Get-ChildItem Services -Recurse -Directory -Include API,Application,Domain,Infrastructure
```

### Bước 2: Inspect endpoint files

```powershell
Get-ChildItem Services -Recurse -Filter *Endpoints.cs
```

### Bước 3: Search dependency smell

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "MongoDB.Driver|Npgsql|Dapper|StackExchange.Redis|Confluent.Kafka|MassTransit|HttpContext|IResult|Results."
```

### Bước 4: Review theo layer

API layer:

```text
Endpoint có mỏng không?
Endpoint có ôm business workflow không?
Endpoint có query DB trực tiếp không?
```

Application layer:

```text
Handler có trả IResult không?
Handler có dùng driver DB trực tiếp không?
Use case có phụ thuộc framework không?
```

Domain layer:

```text
Domain có biết MongoDB/Kafka/RabbitMQ không?
Domain có phụ thuộc ASP.NET không?
```

Infrastructure layer:

```text
Có implement port/interface không?
Có leak detail lên Application không?
```

### Bước 5: Viết report

Tạo:

```text
docs/architecture/clean-architecture-review-day-31.md
docs/architecture/service-boundary-review.md
docs/backlog/day-31-architecture-hardening-backlog.md
```

## 5. Kết quả kỳ vọng

Kết quả kỳ vọng:

```text
Biết service nào boundary ổn.
Biết file nào có smell.
Biết lỗi nào chỉ document, lỗi nào nên backlog.
Không refactor rộng trong Day 31.
```

## 6. Lỗi hay gặp

```text
Lỗi 1: Thấy smell là refactor ngay quá nhiều.
Lỗi 2: Gộp Day 32 error format vào Day 31.
Lỗi 3: Đòi mọi service Clean Architecture hoàn hảo ngay.
Lỗi 4: Không phân biệt training-stage exception với lỗi production.
```

## 7. Tổng kết bài học

Day 31 giúp mình học cách đọc kiến trúc thay vì chỉ đọc code. Đây là nền để các bài sau hardening API, DB, validation, messaging có định hướng hơn.

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
Commit: Day 31: Clean Architecture Hexagonal Review
Tag: day-31-clean-architecture-hexagonal-review
```
