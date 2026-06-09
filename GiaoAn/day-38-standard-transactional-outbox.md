---
day: 38
title: "Standard Transactional Outbox"
duration: "90-120 minutes"
phase: "Stage 2 - Reliable Event-Driven Advanced"
project: "MicroShop"
testing: "Outbox code inspection + transaction boundary review + smoke checks"
type: "lesson"
repo_aware: true
source_of_truth: true
encoding_note: "ASCII-safe Markdown"
---

# Day 38: Standard Transactional Outbox

## 0. Current position

You completed:

```text
Day 37: CloudEvents + Event Envelope
```

Day 38 continues Phase 2.2.

Correct roadmap:

```text
Day 37: CloudEvents / Event Envelope
Day 38: Standard Transactional Outbox
Day 39: Outbox Publisher + Advanced Idempotency + Inbox/WebhookLog
```

Day 38 focuses on the transactional write side:

```text
Business data and outbox message are saved in the same database transaction.
```

Day 39 will focus on robust publisher, idempotency, Inbox, and WebhookLog.

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


Current RabbitMQ workflow:

```text
OrderingService
-> Outbox basics
-> RabbitMQ
-> NotificationWorker
```

Important:

```text
RabbitMQ remains workflow/task messaging.
Kafka remains stream/projection learning.
Do not add OrderingService Kafka publisher today.
```

---

## 2. Goal

By the end:

```text
[ ] Existing OrderingService outbox implementation is inspected.
[ ] Current transaction boundary is verified and documented.
[ ] Standard transactional outbox rules are documented.
[ ] Current strong points are documented.
[ ] Remaining gaps between current outbox and standard production outbox are documented.
[ ] Minimal code hardening is applied only if safe.
[ ] A Day 39 publisher/idempotency backlog is created.
```

Main outputs:

```text
docs/messaging/transactional-outbox-standard.md
docs/messaging/outbox-transaction-review-day-38.md
docs/backlog/day-38-transactional-outbox-backlog.md
```

Optional code outputs:

```text
Small OrderingService cleanup only if safe.
Do not assume transaction boundary is missing.
```

---

## 3. Scope guard

Do:

```text
[ ] Inspect OrderingService order write and outbox insert.
[ ] Verify and document that both happen in the same DB transaction.
[ ] Document current outbox schema/fields.
[ ] Document standard transactional outbox rules.
[ ] Create backlog for Day 39 publisher hardening.
```

Do not:

```text
[ ] Do not implement Kafka publisher.
[ ] Do not rewrite outbox publisher.
[ ] Do not add Inbox/WebhookLog yet.
[ ] Do not claim exactly-once.
[ ] Do not change RabbitMQ/Kafka responsibilities.
[ ] Do not introduce distributed transactions.
```

What this proves:

```text
MicroShop understands the transaction boundary required by Outbox Pattern.
```

What this does not prove:

```text
Outbox publisher is fully robust.
Consumers are fully idempotent.
Inbox/WebhookLog is implemented.
```

---

## 4. Pre-check

Inspect OrderingService:

```powershell
Get-ChildItem Services/OrderingService -Recurse -Filter *.cs |
  Select-String -Pattern "Outbox|Transaction|BeginTransaction|Commit|Rollback|UnitOfWork|Npgsql|Dapper|Publish|OrderCreatedIntegrationEvent"
```

Inspect migrations/schema:

```powershell
Get-ChildItem Services/OrderingService -Recurse -Directory -Filter Migrations
Get-ChildItem Services/OrderingService -Recurse -Filter *.sql
Get-ChildItem Services/OrderingService -Recurse -Filter *DatabaseInitializer*.cs
```

Inspect publisher:

```powershell
Get-ChildItem Services/OrderingService -Recurse -Filter *.cs |
  Select-String -Pattern "BackgroundService|HostedService|OutboxPublisher|MassTransit|RabbitMQ|Publish"
```

Inspect contracts:

```powershell
Get-ChildItem BuildingBlocks.Contracts -Recurse -Filter *.cs |
  Select-String -Pattern "OrderCreated|IntegrationEvent|EventId|OccurredAt"
```

---

## 5. Current repo-aware expectation

Expected finding from current repo:

```text
OrderingService checkout currently saves order and outbox message in the same DB transaction.
CheckoutHandler uses _unitOfWork.ExecuteAsync(transaction => ...).
DapperOrderingUnitOfWork provides BeginTransaction, Commit, and Rollback.
```

Day 38 should verify and document this behavior instead of assuming it is missing.

Current publisher is also not blank/basic.

Expected existing DapperOutboxRepository capabilities:

```text
ClaimPendingAsync
RetryCount
LastError
NextAttemptAtUtc
LockId
LockedUntilUtc
MarkAsProcessedAsync
MarkAsFailedAsync
FOR UPDATE SKIP LOCKED
```

Day 38 should document these current strengths and then identify remaining gaps.

Day 39 should review and harden the existing publisher, then add advanced idempotency, Inbox, and WebhookLog direction.

---

## 6. Standard Transactional Outbox rules

Create:

```text
docs/messaging/transactional-outbox-standard.md
```

Include:

```text
Goal:
Avoid losing integration events when business data is saved but broker publish fails.

Core rule:
Business data and outbox message must be saved in the same database transaction.

Standard flow:
1. Validate command.
2. Begin DB transaction.
3. Save business data.
4. Insert outbox message in same DB transaction.
5. Commit transaction.
6. Background publisher reads pending outbox messages.
7. Publisher sends to broker.
8. Publisher marks message as processed or failed.

Delivery guarantee:
At-least-once delivery.
Consumers must be idempotent.
Exactly-once is not guaranteed.

Not in Day 38:
Advanced publisher retry.
Inbox.
WebhookLog.
Kafka publisher.
```

---

## 7. Outbox schema review

Document actual schema.

Actual fields to verify in current OrderingService outbox schema:

```text
Id
OccurredAtUtc
Type
Content
NextAttemptAtUtc
ProcessedAtUtc
RetryCount
LastError
LockId
LockedUntilUtc
```

Do not invent fields.

Current notes:

```text
No separate Status column yet.
Status is inferred from ProcessedAtUtc, RetryCount, LastError, LockId, and LockedUntilUtc.
No separate CreatedAtUtc column yet.
OccurredAtUtc is currently the event/outbox occurrence timestamp.
```

If additional fields are needed, document as backlog.

---

## 8. Transaction boundary review

Create:

```text
docs/messaging/outbox-transaction-review-day-38.md
```

Include:

```text
Reviewed flow:
OrderingService order write
Outbox insert
Outbox publisher
RabbitMQ publish
NotificationWorker consume

Transaction boundary table:
Step | Current behavior | Expected standard | Gap
Save order | TBD | Inside transaction | TBD
Insert outbox | TBD | Same transaction as order | TBD
Commit | TBD | After both succeed | TBD
Publish broker | TBD | After commit by background publisher | TBD

What this proves:
We know whether OrderingService uses standard transactional outbox boundaries.

What this does not prove:
Publisher retry/idempotency is complete.
Inbox/WebhookLog exists.
```

Fill TBD after code inspection.

---

## 9. Minimal code change policy

Only apply code change if:

```text
[ ] Current transaction boundary is clearly wrong after verification.
[ ] Fix is small and local.
[ ] Behavior can be tested.
[ ] It does not require publisher rewrite.
```

Allowed:

```text
Improve documentation/logging around outbox transaction if needed.
Add comments/tests around existing _unitOfWork.ExecuteAsync behavior if useful.
Add missing outbox status documentation only if clearly needed and safe.
```

Avoid:

```text
Changing event contract.
Changing broker behavior.
Adding Kafka publisher.
Adding Inbox/WebhookLog.
Rewriting all persistence.
```

If unsure:

```text
Document the gap and defer implementation.
```

---

## 10. Runtime smoke checks

Full system may be needed for Ordering/RabbitMQ flow:

```powershell
docker compose up -d --build
```

Or targeted if dependencies allow:

```powershell
docker compose up -d --build postgres rabbitmq orderingservice notificationworker
```

Check logs:

```powershell
docker compose logs orderingservice --tail 100
docker compose logs notificationworker --tail 100
docker compose logs rabbitmq --tail 100
```

Check endpoint:

```text
POST /orders/checkout
GET /debug/outbox
```

Use actual request body from current Postman/docs.

Do not invent checkout payload.

---

## 11. Day 39 handoff notes

Create backlog for next day:

```text
Day 39: Outbox Publisher + Advanced Idempotency + Inbox/WebhookLog
```

Day 39 should handle:

```text
Review and harden existing publisher state transitions.
Existing RetryCount / LastError / NextAttemptAtUtc / locking behavior.
Processed messages / Inbox.
WebhookLog for Payment webhooks.
Idempotency key / eventId checks.
```

---

## 12. Backlog

Create:

```text
docs/backlog/day-38-transactional-outbox-backlog.md
```

Include:

```text
Transaction boundary:
[ ] Verify order save and outbox insert already use same DB transaction.
[ ] Document CheckoutHandler _unitOfWork.ExecuteAsync path.
[ ] Document DapperOrderingUnitOfWork BeginTransaction/Commit/Rollback behavior.

Schema:
[ ] Document outbox table fields.
[ ] Add status/retry/error fields if missing and planned.
[ ] Add index for pending outbox messages if needed.

Day 39 handoff:
[ ] Review existing publisher retry/failure states.
[ ] Review ClaimPendingAsync and FOR UPDATE SKIP LOCKED behavior.
[ ] Advanced idempotency.
[ ] Inbox/processed messages.
[ ] WebhookLog for PaymentService.
```

---

## 13. Build/test plan

Build:

```powershell
dotnet build Services/OrderingService/OrderingService.csproj
dotnet build Services/NotificationWorker/NotificationWorker.csproj
dotnet build BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj
```

If code changed:

```powershell
dotnet build
```

Runtime smoke if feasible:

```powershell
docker compose up -d --build
```

---

## 14. Docs update

Update:

```text
docs/README.md
```

Link Day 38 docs.

---

## 15. Production fit review

What this improves:

```text
Outbox transaction boundary becomes explicit.
Reliability risks are easier to discuss.
Day 39 publisher/idempotency work has a clear base.
```

What remains future work:

```text
Further publisher hardening.
Inbox.
WebhookLog.
Consumer idempotency.
Monitoring stuck outbox messages.
```

Do not claim:

```text
Exactly-once delivery.
Fully production-grade outbox.
Kafka publishing from OrderingService.
```

---

## 16. Pass checklist

```text
[ ] OrderingService outbox code is inspected.
[ ] Existing transaction boundary is verified and documented.
[ ] Current publisher capabilities are documented.
[ ] transactional-outbox-standard.md exists.
[ ] outbox-transaction-review-day-38.md exists.
[ ] day-38 backlog exists.
[ ] Runtime smoke is performed or skipped with reason.
[ ] Build passes for reviewed/touched projects or failures are documented.
[ ] No Kafka publisher is added.
[ ] Day 39 handoff is clear.
```

---

## 17. Optional commit/tag after review

```text
Commit: Day 38: Standard Transactional Outbox
Tag: day-38-standard-transactional-outbox
```

---

## 18. Next day

```text
Day 39: Outbox Publisher + Advanced Idempotency + Inbox/WebhookLog
```
