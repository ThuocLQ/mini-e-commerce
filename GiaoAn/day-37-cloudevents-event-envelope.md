---
day: 37
title: "CloudEvents + Event Envelope"
duration: "90-120 minutes"
phase: "Stage 2 - Reliable Event-Driven Advanced"
project: "MicroShop"
testing: "Contract review + sample event validation + Kafka/RabbitMQ docs"
type: "lesson"
repo_aware: true
source_of_truth: true
encoding_note: "ASCII-safe Markdown"
---

# Day 37: CloudEvents + Event Envelope

## 0. Current position

You completed:

```text
Day 36: Advanced Strategy Pattern + Audit Log + Advanced Identity Review
```

Day 37 starts Phase 2.2: Reliable Event-Driven Advanced.

Correct roadmap:

```text
Day 37: CloudEvents / Event Envelope
Day 38: Standard Transactional Outbox
Day 39: Outbox Publisher + Advanced Idempotency + Inbox/WebhookLog
Day 40: Kafka Consumer Group + Rebalance
Day 41: MongoDB Projection Rebuild
```

Day 37 is not about implementing Kafka DLT yet.

It focuses on:

```text
CloudEvents mindset
standard event envelope
metadata fields
event versioning direction
RabbitMQ/Kafka contract consistency
```

---

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

Workers:
- Services/NotificationWorker
- Workers/ProjectionWorker

Shared:
- BuildingBlocks.Contracts
- MicroShop.AppHost
- MicroShop.ServiceDefaults
```

Never use:

```text
/orders/read-model
ORD-900
CUST-900
```


Current flows:

```text
RabbitMQ workflow:
OrderingService
-> Outbox basics
-> RabbitMQ
-> NotificationWorker

Kafka projection demo:
Kafka CLI demo producer
-> topic microshop.order-events
-> ProjectionWorker
-> MongoDB MicroShop_OrderReadDb.order_summaries
-> OrderQueryService
```

Important:

```text
RabbitMQ asks: who should process this work?
Kafka asks: who wants to observe this event stream?
```

Do not say Kafka replaced RabbitMQ.

Do not say OrderingService publishes Kafka unless code has been implemented and verified.

---

## 2. Goal

By the end:

```text
[ ] Current event contracts are inspected.
[ ] CloudEvents concept is understood.
[ ] A MicroShop event envelope standard is documented.
[ ] Existing RabbitMQ/Kafka event payloads are compared against the standard.
[ ] Versioning/backward compatibility rules for events are documented.
[ ] A migration/backlog plan is created.
```

Main outputs:

```text
docs/messaging/event-envelope-standard.md
docs/messaging/cloudevents-review-day-37.md
docs/backlog/day-37-event-envelope-backlog.md
```

Optional code outputs:

```text
None by default.
Design-first unless repo is ready for a small sample envelope type.
```

---

## 3. Scope guard

Do:

```text
[ ] Inspect current contracts.
[ ] Document envelope standard.
[ ] Compare current events to target envelope.
[ ] Add sample event JSON.
[ ] Create backlog.
```

Do not:

```text
[ ] Do not rewrite all event contracts today.
[ ] Do not change RabbitMQ/Kafka runtime behavior today.
[ ] Do not add Schema Registry today.
[ ] Do not add Kafka DLT today.
[ ] Do not implement OrderingService Kafka publisher today.
[ ] Do not claim CloudEvents compliance if not fully implemented.
```

What this proves:

```text
MicroShop has an event contract governance direction.
```

What this does not prove:

```text
All events are CloudEvents-compliant.
Schema Registry is implemented.
Event versioning is enforced at runtime.
```

---

## 4. Pre-check

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

## 5. CloudEvents mental model

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
Use CloudEvents as a reference standard.
Document MicroShop event envelope.
Do not force full runtime migration today.
```

---

## 6. MicroShop event envelope standard

Create:

```text
docs/messaging/event-envelope-standard.md
```

Include:

```text
Goal:
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

## 7. Compare current RabbitMQ and Kafka payloads separately

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

Important distinction:

```text
RabbitMQ IntegrationEvent already has Version.
Kafka demo payload currently does not have eventVersion.
RabbitMQ OrderCreatedIntegrationEvent does not include customerName/itemCount/items.
Kafka projection demo payload does include customerName/itemCount/items.
```

This is acceptable for training-stage demo.

Document as backlog.

---

## 8. CloudEvents review doc

Create:

```text
docs/messaging/cloudevents-review-day-37.md
```

Include:

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

Decision:
Use CloudEvents as reference.
Create MicroShop envelope standard.
Do not migrate all runtime events on Day 37.
```

---

## 9. Optional sample contract design

Do not implement unless approved.

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

Rules:

```text
Keep it in BuildingBlocks.Contracts only if all consumers can adopt it gradually.
Do not break current ProjectionWorker payload.
Do not break NotificationWorker consumer.
```

---

## 10. Runtime verification

No runtime required if docs only.

Optional builds:

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

Create:

```text
docs/backlog/day-37-event-envelope-backlog.md
```

Include:

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

## 12. Docs update

Update:

```text
docs/README.md
```

Link Day 37 docs.

---

## 13. Production fit review

What this improves:

```text
Event metadata becomes intentional.
Future tracing/replay/versioning becomes easier.
RabbitMQ/Kafka contracts have a shared direction.
```

What remains future work:

```text
Runtime envelope migration.
Schema Registry.
Contract tests.
Event compatibility checks.
```

---

## 14. Pass checklist

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

## 15. Optional commit/tag after review

```text
Commit: Day 37: CloudEvents Event Envelope
Tag: day-37-cloudevents-event-envelope
```

---

## 16. Next day

```text
Day 38: Standard Transactional Outbox
```
