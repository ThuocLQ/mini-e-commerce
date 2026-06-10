---
day: 37
title: "CloudEvents + Event Envelope"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
encoding_note: "UTF-8 Markdown tiếng Việt chuẩn"
style: "learning-practice"
---

# Day 37: CloudEvents + Event Envelope

## 0. Hôm nay học gì?

Hôm nay mình học cách chuẩn hóa event metadata bằng CloudEvents/Event Envelope.

Bài này chưa migrate toàn bộ runtime payload. Trọng tâm là hiểu chuẩn và review contract hiện có.

## 1. Vì sao cần bài này?

Event-driven system rất dễ rối nếu mỗi event có một shape khác nhau. Event Envelope giúp event có metadata ổn định: eventId, type, source, version, correlationId...

Nhờ đó sau này dễ trace, replay, versioning và debug hơn.

## 2. Khái niệm cốt lõi

### CloudEvents

CloudEvents là chuẩn mô tả metadata của event.

Field hay gặp:

```text
id
source
specversion
type
time
subject
datacontenttype
data
```

### Event Envelope

Envelope là lớp bọc quanh payload nghiệp vụ.

```text
metadata + data
```

Ví dụ:

```json
{
  "eventId": "...",
  "eventType": "com.microshop.order.created.v1",
  "source": "microshop.ordering-service",
  "occurredAtUtc": "...",
  "data": {
    "orderId": "..."
  }
}
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


Repo-aware:

```text
Repo đã có BuildingBlocks.Contracts/Events/MicroShopEventEnvelope.cs.
Day 37 không tạo khái niệm envelope từ số 0.
Day 37 review/chuẩn hóa contract/envelope hiện có.
Không bắt buộc migrate toàn bộ RabbitMQ/Kafka runtime payload hôm nay.
```

RabbitMQ base `IntegrationEvent` hiện có:

```text
EventId
OccurredAtUtc
Version
```

Kafka demo payload chưa có `eventVersion` field.

## 4. Thực hành từng bước

### Bước 1: Inspect contracts

```powershell
Get-ChildItem BuildingBlocks.Contracts -Recurse -Filter *.cs |
  Select-String -Pattern "IntegrationEvent|MicroShopEventEnvelope|OrderCreated|EventId|OccurredAt|Version"
```

### Bước 2: Inspect producers/consumers

```powershell
Get-ChildItem Services/OrderingService -Recurse -Filter *.cs |
  Select-String -Pattern "Outbox|Publish|OrderCreatedIntegrationEvent|IntegrationEvent"

Get-ChildItem Workers/ProjectionWorker -Recurse -Filter *.cs |
  Select-String -Pattern "eventId|eventType|orderId|customerId|customerName|occurredAtUtc|projection_failures"
```

### Bước 3: So sánh RabbitMQ và Kafka riêng

RabbitMQ `OrderCreatedIntegrationEvent` tối giản:

```text
OrderId
CustomerId
TotalAmount
Currency
```

Kafka demo payload có:

```text
customerName
itemCount
items
```

### Bước 4: Tạo docs

```text
docs/messaging/event-envelope-standard.md
docs/messaging/cloudevents-review-day-37.md
docs/backlog/day-37-event-envelope-backlog.md
```

## 5. Kết quả kỳ vọng

Kỳ vọng:

```text
Hiểu CloudEvents dùng để làm gì.
Biết repo đã có MicroShopEventEnvelope.
RabbitMQ và Kafka payload được review riêng.
Không viết sai rằng RabbitMQ thiếu Version.
Có backlog migrate envelope dần.
```

## 6. Lỗi hay gặp

```text
Lỗi 1: Trộn RabbitMQ contract và Kafka demo payload.
Lỗi 2: Nói current events thiếu eventVersion trong khi RabbitMQ IntegrationEvent đã có Version.
Lỗi 3: Ép migrate toàn bộ payload runtime hôm nay.
Lỗi 4: Claim fully CloudEvents-compliant.
```

## 7. Tổng kết bài học

Day 37 giúp mình học tư duy contract governance trong event-driven system. Event không chỉ có payload, mà còn cần metadata để trace, version và replay.

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
Commit: Day 37: CloudEvents Event Envelope
Tag: day-37-cloudevents-event-envelope
```
