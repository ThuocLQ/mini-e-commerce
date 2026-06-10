---
day: 38
title: "Standard Transactional Outbox"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
encoding_note: "UTF-8 Markdown tiếng Việt chuẩn"
style: "learning-practice"
---

# Day 38: Standard Transactional Outbox

## 0. Hôm nay học gì?

Hôm nay mình học Outbox Pattern ở phần quan trọng nhất: transaction boundary.

Business data và outbox message phải được lưu trong cùng một DB transaction.

## 1. Vì sao cần bài này?

Nếu order lưu DB thành công nhưng publish message lên broker fail, hệ thống có thể mất event. Transactional Outbox giải quyết bằng cách lưu event vào DB trước, publisher gửi sau.

Đây là pattern rất hay được dùng trong microservices thực tế.

## 2. Khái niệm cốt lõi

### Vấn đề không có Outbox

```text
Save order thành công.
Publish RabbitMQ fail.
Notification không bao giờ được gửi.
```

### Với Outbox

```text
Save order thành công.
Outbox row cũng được lưu cùng transaction.
Publisher retry sau.
```

### Delivery guarantee

Outbox thường là:

```text
At-least-once delivery
```

Nên consumer phải idempotent. Không claim exactly-once.

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
OrderingService checkout đang lưu order và outbox trong _unitOfWork.ExecuteAsync(transaction => ...).
DapperOrderingUnitOfWork có BeginTransaction, Commit, Rollback.
DapperOutboxRepository đã có ClaimPendingAsync, RetryCount, LastError, NextAttemptAtUtc, LockId, LockedUntilUtc, MarkAsProcessedAsync, MarkAsFailedAsync, FOR UPDATE SKIP LOCKED.
```

Day 38 verify/document, không viết như thể outbox còn trắng.

## 4. Thực hành từng bước

### Bước 1: Inspect transaction boundary

```powershell
Get-ChildItem Services/OrderingService -Recurse -Filter *.cs |
  Select-String -Pattern "Outbox|Transaction|BeginTransaction|Commit|Rollback|UnitOfWork|Npgsql|Dapper|Publish|OrderCreatedIntegrationEvent"
```

### Bước 2: Inspect publisher/repository

```powershell
Get-ChildItem Services/OrderingService -Recurse -Filter *.cs |
  Select-String -Pattern "BackgroundService|HostedService|OutboxPublisher|MassTransit|RabbitMQ|Publish|ClaimPendingAsync|NextAttemptAtUtc|FOR UPDATE SKIP LOCKED"
```

### Bước 3: Document standard outbox

Tạo:

```text
docs/messaging/transactional-outbox-standard.md
```

Flow chuẩn:

```text
1. Validate command.
2. Begin DB transaction.
3. Save business data.
4. Insert outbox message trong cùng transaction.
5. Commit transaction.
6. Background publisher đọc pending outbox.
7. Publisher gửi message lên RabbitMQ.
8. Publisher mark processed hoặc failed.
```

### Bước 4: Document review hiện tại

Tạo:

```text
docs/messaging/outbox-transaction-review-day-38.md
docs/backlog/day-38-transactional-outbox-backlog.md
```

## 5. Kết quả kỳ vọng

Kỳ vọng:

```text
Transaction boundary được verify.
Current publisher capabilities được document.
Không thêm Kafka publisher.
Day 39 handoff rõ ràng: harden existing publisher + idempotency + Inbox/WebhookLog.
```

## 6. Lỗi hay gặp

```text
Lỗi 1: Viết như thể OrderingService chưa có transaction boundary.
Lỗi 2: Viết publisher còn basic dù đã có ClaimPendingAsync/lock/retry.
Lỗi 3: Claim exactly-once.
Lỗi 4: Thêm Kafka publisher vào OrderingService.
```

## 7. Tổng kết bài học

Day 38 giúp mình hiểu outbox không chỉ là một table. Điều quan trọng nhất là business data và outbox message nằm trong cùng transaction, sau đó publisher xử lý theo at-least-once.

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
Commit: Day 38: Standard Transactional Outbox
Tag: day-38-standard-transactional-outbox
```
