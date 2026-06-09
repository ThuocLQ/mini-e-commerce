# Day 37: CloudEvents + Event Envelope

## 0. Vị trí hiện tại

Bạn đã hoàn thành:

```text
Day 36: Advanced Strategy Pattern + Audit Log + Advanced Identity Review
```

Day 37 bắt đầu Phase 2.2: Reliable Event-Driven Advanced.

Roadmap đúng:

```text
Day 37: CloudEvents / Event Envelope
Day 38: Standard Transactional Outbox
Day 39: Outbox Publisher + Advanced Idempotency + Inbox/WebhookLog
Day 40: Kafka Consumer Group + Rebalance
Day 41: MongoDB Projection Rebuild
```

Day 37 chưa triển khai Kafka DLT.

Bài này tập trung vào:

```text
CloudEvents mindset
standard event envelope
metadata fields
event versioning direction
RabbitMQ/Kafka contract consistency
```

---

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

Workers:
- Services/NotificationWorker
- Workers/ProjectionWorker

Shared:
- BuildingBlocks.Contracts
- MicroShop.AppHost
- MicroShop.ServiceDefaults
```

Không dùng:

```text
/orders/read-model
ORD-900
CUST-900
```


Flow hiện tại:

```text
Workflow RabbitMQ:
OrderingService
-> Outbox basics
-> RabbitMQ
-> NotificationWorker

Demo projection Kafka:
Kafka CLI demo producer
-> topic microshop.order-events
-> ProjectionWorker
-> MongoDB MicroShop_OrderReadDb.order_summaries
-> OrderQueryService
```

Lưu ý quan trọng:

```text
RabbitMQ asks: who should process this work?
Kafka asks: who wants to observe this event stream?
```

Không nói Kafka thay thế RabbitMQ.

Không nói OrderingService publish Kafka nếu code chưa được implement và verify.

---

## 2. Mục tiêu

Sau khi hoàn thành:

```text
[ ] Current event contracts are inspected.
[ ] CloudEvents concept is understood.
[ ] A MicroShop event envelope standard is documented.
[ ] Existing RabbitMQ/Kafka event payloads are compared against the standard.
[ ] Versioning/backward compatibility rules for events are documented.
[ ] A migration/backlog plan is created.
```

Output chính:

```text
docs/messaging/event-envelope-standard.md
docs/messaging/cloudevents-review-day-37.md
docs/backlog/day-37-event-envelope-backlog.md
```

Output code tùy chọn:

```text
None by default.
Design-first unless repo is ready for a small sample envelope type.
```

---

## 3. Giới hạn phạm vi

Nên làm:

```text
[ ] Inspect current contracts.
[ ] Document envelope standard.
[ ] Compare current events to target envelope.
[ ] Add sample event JSON.
[ ] Create backlog.
```

Không làm:

```text
[ ] Do not rewrite all event contracts today.
[ ] Do not change RabbitMQ/Kafka runtime behavior today.
[ ] Do not add Schema Registry today.
[ ] Do not add Kafka DLT today.
[ ] Do not implement OrderingService Kafka publisher today.
[ ] Do not claim CloudEvents compliance if not fully implemented.
```

Điều phần này chứng minh:

```text
MicroShop has an event contract governance direction.
```

Điều phần này chưa chứng minh:

```text
All events are CloudEvents-compliant.
Schema Registry is implemented.
Event versioning is enforced at runtime.
```

---

## 4. Kiểm tra trước khi làm

Inspect contracts:

```powershell
Get-ChildItem BuildingBlocks.Contracts -Recurse -Filter *.cs |
  Select-String -Pattern "IntegrationEvent|OrderCreated|OrderPaid|OrderCancelled|EventId|OccurredAt"
```

Inspect producers/consumers:

```powershell
Get-ChildItem Services/OrderingService -Recurse -Filter *.cs |
  Select-String -Pattern "Outbox|Publish|OrderCreatedIntegrationEvent|IntegrationEvent"

Get-ChildItem Services/NotificationWorker -Recurse -Filter *.cs |
  Select-String -Pattern "Consumer|OrderCreatedIntegrationEvent|MassTransit"

Get-ChildItem Workers/ProjectionWorker -Recurse -Filter *.cs |
  Select-String -Pattern "eventId|eventType|orderId|customerId|customerName|occurredAtUtc|projection_failures"
```

---

## 5. Tư duy CloudEvents

CloudEvents is a standard way to describe event metadata.

Core metadata concepts:

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

Example shape:

```json
{
  "specversion": "1.0",
  "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "source": "microshop.ordering-service",
  "type": "com.microshop.order.created.v1",
  "time": "2026-05-28T10:00:00Z",
  "subject": "orders/11111111-1111-1111-1111-111111111111",
  "datacontenttype": "application/json",
  "data": {
    "orderId": "11111111-1111-1111-1111-111111111111",
    "customerId": "22222222-2222-2222-2222-222222222222",
    "customerName": "Demo Customer",
    "totalAmount": 1977.3,
    "currency": "VND",
    "itemCount": 0,
    "items": []
  }
}
```

Day 37 target:

```text
Dùng CloudEvents như chuẩn tham chiếu.
Document event envelope của MicroShop.
Không ép migrate runtime toàn bộ trong hôm nay.
```

---

## 6. Chuẩn event envelope của MicroShop

Tạo:

```text
docs/messaging/event-envelope-standard.md
```

Bao gồm:

```text
Mục tiêu:
Events should have consistent metadata for tracing, versioning, replay, and debugging.

Envelope fields:
eventId
eventType
eventVersion
source
occurredAtUtc
correlationId
causationId
subject
data

CloudEvents mapping:
eventId -> id
eventType -> type
source -> source
occurredAtUtc -> time
subject -> subject
data -> data

Recommended type style:
com.microshop.order.created.v1
com.microshop.order.paid.v1
com.microshop.order.cancelled.v1

Compatibility rules:
Add optional fields when possible.
Do not rename fields without versioning.
Do not change field type without versioning.
Do not remove fields without a migration plan.
```

---

## 7. So sánh riêng payload RabbitMQ và Kafka hiện tại

Current RabbitMQ base contract:

```text
BuildingBlocks.Contracts.Events.IntegrationEvent already has:
- EventId
- OccurredAtUtc
- Version
```

Current RabbitMQ OrderCreatedIntegrationEvent is intentionally minimal:

```text
OrderId
CustomerId
TotalAmount
Currency
```

RabbitMQ contract gaps vs CloudEvents-style envelope:

```text
No source.
No correlationId.
No causationId.
No subject.
No CloudEvents-style namespaced type.
No nested data object.
```

Current valid Kafka projection demo payload:

```json
{
  "eventId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "eventType": "OrderCreated",
  "orderId": "11111111-1111-1111-1111-111111111111",
  "customerId": "22222222-2222-2222-2222-222222222222",
  "customerName": "Demo Customer",
  "totalAmount": 1977.3,
  "currency": "VND",
  "itemCount": 0,
  "items": [],
  "occurredAtUtc": "2026-05-28T10:00:00Z"
}
```

Kafka demo payload gaps vs envelope:

```text
No source.
No eventVersion field.
No correlationId.
No causationId.
No subject.
Data is flattened instead of nested.
eventType is simple name, not namespaced.
```

Phân biệt quan trọng:

```text
RabbitMQ IntegrationEvent already has Version.
Kafka demo payload currently does not have eventVersion.
RabbitMQ OrderCreatedIntegrationEvent does not include customerName/itemCount/items.
Kafka projection demo payload does include customerName/itemCount/items.
```

Điều này chấp nhận được với demo ở giai đoạn training.

Ghi lại vào backlog.

---

## 8. Tài liệu review CloudEvents

Tạo:

```text
docs/messaging/cloudevents-review-day-37.md
```

Bao gồm:

```text
Reviewed current events:
RabbitMQ OrderCreatedIntegrationEvent
Kafka demo event: OrderCreated / OrderPaid / OrderCancelled

Findings table:
RabbitMQ IntegrationEvent base has EventId, OccurredAtUtc, Version.
RabbitMQ OrderCreatedIntegrationEvent has OrderId, CustomerId, TotalAmount, Currency.
Kafka projection demo has eventId, eventType, orderId, customerId, customerName, totalAmount, currency, itemCount, items, occurredAtUtc.
RabbitMQ and Kafka payloads should be reviewed separately.
source missing/current
correlationId missing/current
causationId missing/current
subject missing/current
CloudEvents type naming missing/current
data nesting flattened/current

Quyết định:
Use CloudEvents as reference.
Create MicroShop envelope standard.
Do not migrate all runtime events on Day 37.
```

---

## 9. Thiết kế contract mẫu tùy chọn

Không implement nếu chưa được duyệt.

Possible generic envelope:

```csharp
public sealed record MicroShopEventEnvelope<TData>(
    Guid EventId,
    string EventType,
    int EventVersion,
    string Source,
    DateTime OccurredAtUtc,
    string? CorrelationId,
    string? CausationId,
    string Subject,
    TData Data);
```

Quy tắc:

```text
Keep it in BuildingBlocks.Contracts only if all consumers can adopt it gradually.
Do not break current ProjectionWorker payload.
Do not break NotificationWorker consumer.
```

---

## 10. Xác minh runtime

Không cần chạy runtime nếu chỉ sửa docs.

Build tùy chọn:

```powershell
dotnet build BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj
dotnet build Services/NotificationWorker/NotificationWorker.csproj
dotnet build Workers/ProjectionWorker/ProjectionWorker.csproj
```

If testing current Kafka demo, use valid Day 30 payload.

Lite projection demo:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

---

## 11. Backlog

Tạo:

```text
docs/backlog/day-37-event-envelope-backlog.md
```

Bao gồm:

```text
[ ] Add event envelope standard docs.
[ ] Add source/eventVersion/correlationId guidance.
[ ] Decide event type naming convention.
[ ] Add envelope type in BuildingBlocks.Contracts.
[ ] Migrate RabbitMQ integration events gradually.
[ ] Migrate Kafka projection events gradually.
[ ] Add contract tests.
[ ] Schema Registry concept later.
```

---

## 12. Cập nhật docs

Update:

```text
docs/README.md
```

Link Day 37 docs.

---

## 13. Review độ phù hợp production-minded

Điều phần này cải thiện:

```text
Event metadata becomes intentional.
Future tracing/replay/versioning becomes easier.
RabbitMQ/Kafka contracts have a shared direction.
```

Những phần còn là future work:

```text
Runtime envelope migration.
Schema Registry.
Contract tests.
Event compatibility checks.
```

---

## 14. Checklist đạt yêu cầu

```text
[ ] Current event contracts are inspected.
[ ] event-envelope-standard.md exists.
[ ] cloudevents-review-day-37.md exists.
[ ] day-37 backlog exists.
[ ] RabbitMQ contract and Kafka demo payload are reviewed separately.
[ ] Current Kafka payload gaps are documented.
[ ] Current RabbitMQ envelope gaps are documented.
[ ] RabbitMQ/Kafka roles remain separate.
[ ] No breaking event contract migration is introduced.
[ ] docs/README.md links new docs.
```

---

## 15. Commit/tag tùy chọn sau review

```text
Commit: Day 37: CloudEvents Event Envelope
Tag: day-37-cloudevents-event-envelope
```

---

## 16. Ngày tiếp theo

```text
Day 38: Standard Transactional Outbox
```

