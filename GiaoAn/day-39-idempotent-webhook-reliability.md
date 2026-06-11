---
day: 39
title: "Idempotent Webhook Reliability"
duration: "90-120 phút"
phase: "Stage 2 - Reliable Event-Driven Advanced"
project: "MicroShop"
testing: "Postman duplicate webhook lab + build verification"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
encoding_note: "UTF-8 Markdown tiếng Việt chuẩn"
---

# Day 39: Idempotent Webhook Reliability

## 0. Hôm nay học gì?

Day 39 nối tiếp Day 38.

Day 38 đã tập trung vào `OrderingService` outbox: business data và outbox message phải nằm trong cùng database transaction.

Day 39 chuyển sang một vấn đề reliability khác rất hay gặp trong production: webhook từ provider có thể gửi lại nhiều lần.

Hôm nay mình thêm idempotency log cho `PaymentService` webhook:

```text
Payment provider
-> POST /payments/webhooks/payment
-> WebhookLogs unique ProviderEventId
-> update Payments đúng một lần về mặt nghiệp vụ
```

## 1. Vì sao cần bài này?

Webhook không giống request người dùng bình thường.

Provider có thể gửi lại cùng một event vì:

```text
Service trả timeout.
Network lỗi giữa đường.
Provider không nhận được response 2xx.
Provider retry theo lịch riêng.
```

Nếu service không idempotent, cùng một webhook có thể gây lỗi nghiệp vụ:

```text
Gửi email thanh toán thành công nhiều lần.
Ghi audit nhiều lần.
Chuyển trạng thái payment sai.
Trigger downstream workflow nhiều lần.
```

Bài này không hứa exactly-once. Mục tiêu thực tế hơn:

```text
Cùng providerEventId được xử lý như cùng một webhook delivery.
Duplicate delivery không làm payment transition lần hai.
Webhook processing có log để debug.
```

## 2. Khái niệm cốt lõi

Idempotency nghĩa là gọi lại cùng một operation với cùng một idempotency key thì kết quả nghiệp vụ không bị nhân đôi.

Với webhook, idempotency key tốt nhất thường là event id do provider gửi:

```text
providerEventId
```

Trong MicroShop, `providerEventId` là optional để giữ backward-compatible với request cũ. Nếu client chưa gửi field này, service tự tạo key ổn định từ:

```text
paymentId + providerTransactionId + status
```

Database bảo vệ bằng unique index:

```text
WebhookLogs.ProviderEventId unique
```

## 3. Nhìn vào repo hiện tại

Các file chính trong bài:

```text
Services/PaymentService/API/Contracts/PaymentWebhookRequest.cs
Services/PaymentService/Application/Payments/Webhooks/PaymentWebhookCommand.cs
Services/PaymentService/Application/Payments/Webhooks/PaymentWebhookHandler.cs
Services/PaymentService/Application/Abstractions/IPaymentWebhookRepository.cs
Services/PaymentService/Infrastructure/Persistence/DapperPaymentWebhookRepository.cs
Services/PaymentService/Infrastructure/Persistence/Migrations/002_CreateWebhookLogs.sql
```

Route test:

```text
POST /payments
GET /payments/{id}
POST /payments/webhooks/payment
POST /webhooks/payment
```

Gateway route vẫn dùng:

```text
{{gatewayBaseUrl}}/payments
{{gatewayBaseUrl}}/payments/webhooks/payment
```

Không đổi RabbitMQ/Kafka trong bài này.

## 4. Thực hành từng bước

### Bước 1: Inspect webhook hiện tại

```powershell
Get-ChildItem Services/PaymentService -Recurse -Filter *.cs |
  Select-String -Pattern "PaymentWebhook|WebhookLog|ProviderEventId|PaymentStatus|FOR UPDATE"
```

Mục tiêu là thấy webhook không còn chỉ update payment trực tiếp.

### Bước 2: Inspect migration

```powershell
Get-Content Services/PaymentService/Infrastructure/Persistence/Migrations/002_CreateWebhookLogs.sql
```

Kiểm tra các điểm chính:

```text
WebhookLogs table
UX_WebhookLogs_ProviderEventId
IX_WebhookLogs_PaymentId
Status = Processing / Processed / Failed
```

### Bước 3: Build PaymentService

```powershell
dotnet build Services\PaymentService\PaymentService.csproj --no-restore --nologo -v minimal
```

Nếu muốn check rộng hơn:

```powershell
dotnet build MicroShop.sln --no-restore --nologo -v minimal
```

### Bước 4: Start runtime cần thiết

Tối thiểu cho lab này:

```powershell
docker compose up -d --build postgres paymentservice api-gateway
```

Nếu gateway chưa chạy, có thể gọi trực tiếp `PaymentService` theo port local của service.

### Bước 5: Import Postman collection

Import:

```text
postman/MicroShop.Day39.WebhookIdempotency.postman_collection.json
```

Set variable:

```text
gatewayBaseUrl = https://localhost:7001
```

### Bước 6: Chạy flow Postman

Chạy theo thứ tự:

```text
01 - Create payment
02 - Send succeeded webhook
03 - Send duplicate succeeded webhook
04 - Get payment after duplicate webhook
```

Kỳ vọng:

```text
01 trả 201 và status Pending.
02 trả 200 và status Succeeded.
03 trả 200 và vẫn status Succeeded.
04 trả 200 và payment không bị đổi sai.
```

### Bước 7: Kiểm tra database

Nếu muốn nhìn trực tiếp log:

```powershell
docker exec microshop-postgres psql -U microshop -d paymentdb -c "select ProviderEventId, PaymentId, EventType, Status, Error, ReceivedAtUtc, ProcessedAtUtc from WebhookLogs order by ReceivedAtUtc desc limit 10;"
```

Kỳ vọng chỉ có một row cho cùng `ProviderEventId`.

## 5. Kết quả kỳ vọng

Sau bài này, mình cần đạt:

```text
Payment webhook có idempotency key.
Duplicate providerEventId không apply transition lần hai.
WebhookLogs ghi được trạng thái Processing/Processed/Failed.
Payment update và webhook log update nằm trong cùng PostgreSQL transaction.
Không claim exactly-once.
Không đổi RabbitMQ/Kafka responsibility.
```

## 6. Lỗi hay gặp

```text
Lỗi 1: Nghĩ unique ProviderEventId là exactly-once. Không phải, đây là idempotency guard.
Lỗi 2: Không gửi providerEventId khi test duplicate rồi dùng providerTransactionId khác, làm key khác.
Lỗi 3: Quên chạy migration nên thiếu table WebhookLogs.
Lỗi 4: Gửi FAILED sau khi payment đã Succeeded, domain sẽ chặn transition ngược.
Lỗi 5: Nhầm Day 39 thành Kafka publisher cho OrderingService.
```

## 7. Tổng kết bài học

Day 39 giúp mình thấy reliability không chỉ nằm ở message broker. HTTP webhook cũng là event delivery và cũng cần idempotency.

Điểm production-minded quan trọng nhất:

```text
ProviderEventId là chìa khóa chống duplicate.
Database unique index là lớp bảo vệ đáng tin.
Domain vẫn quyết định transition hợp lệ.
```

## 8. Checklist trước khi commit

```text
[ ] Hiểu được vì sao webhook có thể bị gửi lại.
[ ] Hiểu được providerEventId dùng để làm gì.
[ ] Build PaymentService pass.
[ ] Postman duplicate webhook flow pass.
[ ] WebhookLogs có unique ProviderEventId.
[ ] Không claim exactly-once.
[ ] Không đổi RabbitMQ/Kafka responsibility.
```

## 9. Commit/tag gợi ý

```text
Commit: Day 39: Idempotent Webhook Reliability
Tag: day-39-idempotent-webhook-reliability
```
