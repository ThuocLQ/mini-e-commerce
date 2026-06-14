---
day: 44
title: "Payment Webhook Reliability + Compensation Trigger"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
style: "production-failure-scenario"
phase: "Stage 2 - Production Hardening"
---

# Day 44: Payment Webhook Reliability + Compensation Trigger

## 0. Vấn đề production

Không viết như WebhookLog chưa có.

Context đúng sau Day 39:

```text
Đã có WebhookLogs cơ bản + ProviderEventId.
```

Day 44 tập trung harden thêm:

```text
Review WebhookLogs hiện có.
Thêm/thiết kế signature verification.
Thêm raw payload hoặc payload hash.
Publish PaymentSucceeded/PaymentFailed một lần.
Kích hoạt compensation khi payment/order state conflict.
```

## 1. Kiến thức cần nắm

### 1.1. Day 39 đã giải quyết một phần

```text
WebhookLogs cơ bản giúp lưu lịch sử webhook.
ProviderEventId giúp chống duplicate ở mức cơ bản.
```

Nhưng production còn cần:

```text
Signature/shared secret/HMAC.
Raw payload hoặc payload hash.
Internal event publish sau khi accept webhook.
Compensation trigger khi webhook đến muộn/conflict.
```

### 1.2. Duplicate webhook

Rule:

```text
(provider, providerEventId) unique.
Duplicate webhook trả response an toàn.
Không publish internal event lần 2.
```

### 1.3. Signature/raw payload

Signature thường cần raw body để verify đúng.

Nếu chưa lưu raw payload, tối thiểu nên lưu:

```text
payloadHash
signatureStatus
receivedAtUtc
processingStatus
```

### 1.4. Compensation trigger

Case quan trọng:

```text
PaymentSucceeded đến sau khi OrderCancelled.
```

Không được tự động mark order Paid nếu business không cho phép.

Có thể:

```text
Mark RefundRequired.
Publish RefundRequested.
Log CompensationRequired.
Manual intervention nếu chưa có refund flow.
```

## 2. Repo cần nhìn vào đâu

```powershell
Get-ChildItem Services/PaymentService -Recurse -Filter *.cs |
  Select-String -Pattern "Webhook|webhooks|WebhookLog|ProviderEventId|PaymentSucceeded|PaymentFailed|Signature|PayloadHash|RawPayload|Outbox|Publish"

Get-ChildItem Services/OrderingService -Recurse -Filter *.cs |
  Select-String -Pattern "PaymentSucceeded|PaymentFailed|OrderStatus|Paid|Cancelled|Refund|Compensation"

Get-ChildItem BuildingBlocks.Contracts -Recurse -Filter *.cs |
  Select-String -Pattern "PaymentSucceeded|PaymentFailed|Refund|IntegrationEvent"
```

Cần xác nhận:

```text
WebhookLogs hiện có field gì?
ProviderEventId có unique chưa?
Có signature verification chưa?
Có raw payload/payload hash chưa?
Có PaymentSucceeded/PaymentFailed contract chưa?
Có publish internal event chưa?
```

## 3. Thực hành chính

### Bước 1: Review WebhookLogs hiện có

Điền bảng:

```text
Capability | Current status | Gap
-----------|----------------|----
WebhookLog exists | yes/no | ?
ProviderEventId stored | yes/no | ?
ProviderEventId unique | yes/no | ?
Duplicate returns safe response | yes/no | ?
Signature verification | yes/no | ?
Raw payload or payload hash | yes/no | ?
Internal event publish | yes/no | ?
Compensation trigger | yes/no | ?
```

### Bước 2: Harden target flow

```text
External Payment Provider
-> POST webhook
-> verify signature/shared secret
-> check ProviderEventId duplicate
-> save/update WebhookLog
-> publish PaymentSucceeded/PaymentFailed once
-> return 2xx fast
```

### Bước 3: Event contract reality

Kiểm tra:

```powershell
Get-ChildItem BuildingBlocks.Contracts -Recurse -Filter *.cs |
  Select-String -Pattern "PaymentSucceeded|PaymentFailed"
```

Nếu chưa có:

```text
PaymentSucceeded/PaymentFailed là contract sẽ tạo nếu implement.
Không nói repo đã có.
```

Contract tối thiểu:

```text
eventId
paymentId
orderId
providerEventId
amount
currency
occurredAtUtc
```

### Bước 4: Signature concept

Training-stage có thể thiết kế hoặc implement shared secret/HMAC.

Pseudo:

```text
Read raw body.
Compute HMAC with shared secret.
Compare with signature header.
If invalid:
    update WebhookLog signature failed
    return 401/400
```

### Bước 5: Publish once rule

Pseudo:

```text
if ProviderEventId already processed:
    return 200

save WebhookLog(received)

publish PaymentSucceeded/PaymentFailed internal event

mark WebhookLog(processed)

return 200
```

### Bước 6: Compensation trigger

Rule:

```text
PaymentSucceeded + OrderPendingPayment:
    mark Paid

PaymentSucceeded + OrderCancelled:
    mark CompensationRequired/RefundRequired

PaymentFailed + OrderPendingPayment:
    cancel order

PaymentFailed + OrderPaid:
    log conflict, không auto cancel
```

## 4. Failure drill

Drill 1:

```text
Gửi cùng ProviderEventId 2 lần.
```

Kỳ vọng:

```text
WebhookLog nhận biết duplicate.
Internal event chỉ publish một lần.
```

Drill 2:

```text
Gửi signature sai.
```

Kỳ vọng:

```text
Không publish PaymentSucceeded/PaymentFailed.
Log signature failed.
```

Drill 3:

```text
PaymentSucceeded đến sau khi OrderCancelled.
```

Kỳ vọng:

```text
Không mark Paid bừa.
Tạo compensation-required/refund-required signal.
```

## 5. Câu hỏi interview

```text
1. Day 39 WebhookLogs đã giải quyết được gì?
2. Day 44 harden thêm gì?
3. Vì sao cần signature verification?
4. Vì sao cần payloadHash/raw payload?
5. Vì sao duplicate webhook vẫn nên return 2xx?
6. Làm sao đảm bảo PaymentSucceeded chỉ publish một lần?
7. PaymentSucceeded sau OrderCancelled xử lý sao?
```

## 6. Kết luận

```text
Day 44 không tạo WebhookLog từ đầu.
Nó review WebhookLogs từ Day 39 và harden phần production còn thiếu:
signature, payload audit, publish event, compensation trigger.
```

## 7. Optional commit

```text
Commit: Day 44: Payment Webhook Reliability Compensation Trigger
Tag: day-44-payment-webhook-reliability
```
