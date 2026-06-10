---
day: 29
title: "README + Architecture Diagram + ADR + API Surface Review"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
encoding_note: "UTF-8 Markdown tiếng Việt chuẩn"
style: "learning-practice"
---

# Day 29: README + Architecture Diagram + ADR + API Surface Review

## 0. Hôm nay học gì?

Hôm nay mình học cách biến repo MicroShop từ “có code chạy được” thành “người khác đọc vào hiểu được hệ thống”.

Bài này không thêm feature. Bài này giúp README, sơ đồ kiến trúc, ADR và API surface khớp với repo thật.

## 1. Vì sao cần bài này?

Một project microservices không chỉ cần code. Nếu tài liệu sai, người review hoặc interviewer sẽ không tin project.

README giống cửa chính của repo. Architecture diagram giống bản đồ. ADR giống nhật ký quyết định kỹ thuật. API surface review giúp biết hệ thống thật sự đang expose route nào.

## 2. Khái niệm cốt lõi

### README là gì?

README trả lời nhanh:

```text
Project này là gì?
Có service nào?
Chạy ra sao?
Luồng chính là gì?
Giới hạn hiện tại là gì?
```

### Architecture diagram là gì?

Sơ đồ kiến trúc không phải để vẽ cho đẹp. Nó giúp nhìn nhanh:

```text
Client gọi Gateway.
Gateway route tới service.
Service dùng DB/cache/broker nào.
Worker consume từ RabbitMQ/Kafka.
```

### ADR là gì?

ADR là Architecture Decision Record. Nó ghi lại quyết định kỹ thuật, ví dụ:

```text
Vì sao RabbitMQ dùng cho workflow?
Vì sao Kafka dùng cho projection?
Vì sao MongoDB dùng cho read model?
```

ADR không cần dài. Quan trọng là ghi được bối cảnh, quyết định và trade-off.

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


Repo hiện có đủ service/worker/hạ tầng để viết README nghiêm túc.

Điểm cần nhớ:

```text
RabbitMQ dùng cho workflow/task messaging.
Kafka dùng cho event stream/projection learning.
OrderQueryService đọc MongoDB read model.
ProjectionWorker consume Kafka.
NotificationWorker consume RabbitMQ.
```

Không được vẽ hoặc viết:

```text
OrderingService -> Kafka
```

nếu code chưa implement và verify.

## 4. Thực hành từng bước

### Bước 1: Kiểm tra repo trước khi viết

```powershell
git status --short
Get-ChildItem docs -Recurse
Get-Content Services/ApiGateway/appsettings.json
Get-Content Services/ApiGateway/appsettings.Docker.json
Get-ChildItem Services -Recurse -Filter *Endpoints.cs
```

### Bước 2: Kiểm tra Swagger/OpenAPI

```powershell
Get-ChildItem . -Recurse -Filter *.cs |
  Select-String -Pattern "AddSwaggerGen|UseSwagger|UseSwaggerUI|AddOpenApi|MapOpenApi|AddEndpointsApiExplorer"
```

Nếu chỉ thấy `AddEndpointsApiExplorer`, ghi:

```text
API surface hiện được review thủ công.
Swagger/OpenAPI UI chưa được bật rõ ràng.
```

### Bước 3: Viết README.md

README nên có:

```text
Current Stage
Services
Workers
Infrastructure
Key Runtime Flows
Important Endpoints
Local Runtime
Current Limitations
Next
```

### Bước 4: Tạo architecture diagram

Tạo:

```text
docs/architecture-diagram.md
```

Sơ đồ cần có:

```text
Client/Postman -> ApiGateway
ApiGateway -> các service
OrderingService -> RabbitMQ -> NotificationWorker
Kafka CLI demo -> Kafka -> ProjectionWorker -> MongoDB -> OrderQueryService
```

### Bước 5: Tạo ADR index

Tạo:

```text
docs/adr/README.md
```

ADR gợi ý:

```text
ADR-001-service-communication.md
ADR-002-rabbitmq-vs-kafka.md
ADR-003-order-read-model-projection.md
```

### Bước 6: Tạo API surface review

Tạo:

```text
docs/api-surface-review.md
```

Ghi theo nhóm:

```text
Catalog
Basket
Ordering
OrderQuery
Discount
Identity
Payment
Health
Debug
```

## 5. Kết quả kỳ vọng

Sau khi làm xong, người đọc repo phải hiểu được:

```text
MicroShop có service nào.
RabbitMQ và Kafka khác vai trò ra sao.
Read model nằm ở đâu.
Route chính là gì.
Repo đang ở stage học, chưa production-ready.
```

## 6. Lỗi hay gặp

```text
Lỗi 1: README nói Swagger UI có nhưng code chưa bật.
Lỗi 2: Diagram vẽ OrderingService publish Kafka dù code chưa có.
Lỗi 3: Vẫn document /orders/read-model.
Lỗi 4: README quá cũ, thiếu PaymentService/OrderQueryService/ProjectionWorker.
Lỗi 5: ADR bị rời rạc, không link với docs cũ.
```

## 7. Tổng kết bài học

Day 29 dạy bạn cách trình bày project như một backend engineer nghiêm túc: code phải chạy, nhưng docs cũng phải thật. Từ bài này, MicroShop có bản đồ để bước sang Day 30 demo/checkpoint.

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
Commit: Day 29: README Architecture ADR API Surface
Tag: day-29-readme-architecture-adr-api-surface
```
