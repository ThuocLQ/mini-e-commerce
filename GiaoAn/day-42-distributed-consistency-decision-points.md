---
day: 42
title: "Distributed Consistency Decision Points"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
style: "production-failure-scenario"
phase: "Stage 2 - Production Hardening"
---

# Day 42: Distributed Consistency Decision Points

## 0. Vấn đề production

Checkout/payment flow đi qua nhiều service. Không có một transaction DB lớn ôm tất cả.

Bài này tạo **Consistency Decision Table** cho MicroShop checkout/payment flow.

## 1. Context sau Day 39

Không được nói như WebhookLog chưa có.

Theo tiến độ hiện tại:

```text
Day 39 đã có WebhookLogs cơ bản + ProviderEventId.
```

Vì vậy Day 42 phải nhìn nhận đúng:

```text
Đã có:
- WebhookLogs cơ bản.
- ProviderEventId để chống duplicate ở mức cơ bản.

Còn cần review/harden:
- Signature/shared secret hoặc HMAC thật.
- Raw payload hoặc payload hash.
- Publish PaymentSucceeded/PaymentFailed event.
- Compensation decision khi payment/order state conflict.
- Saga state rõ hơn.
```

Nếu repo thật khác với dòng trên, ưu tiên repo thật.

## 2. Kiến thức cần nắm

```text
Local transaction:
    Transaction chỉ trong DB của một service.

Distributed consistency:
    Không dùng một transaction lớn xuyên Basket/Order/Payment.

Outbox:
    Bảo vệ case DB commit thành công nhưng publish event fail.

Saga:
    Điều phối workflow nhiều bước qua nhiều service.

Compensation:
    Hành động nghiệp vụ bù trừ, không phải rollback DB.

Idempotency:
    Chống xử lý lặp command/event/webhook.
```

## 3. Repo cần nhìn vào đâu

```powershell
Get-ChildItem Services/OrderingService -Recurse -Filter *.cs |
  Select-String -Pattern "Checkout|Order|OrderStatus|Outbox|Transaction|UnitOfWork|Payment|Basket|Discount"

Get-ChildItem Services/PaymentService -Recurse -Filter *.cs |
  Select-String -Pattern "Payment|PaymentStatus|Webhook|WebhookLog|ProviderEventId|Succeeded|Failed|Idempotency|EventId|Status"
```

Cần xác nhận:

```text
Order hiện có status nào?
Payment hiện có status nào?
WebhookLogs hiện lưu field nào?
ProviderEventId unique chưa?
Webhook đã publish internal event chưa?
Outbox nằm ở đâu?
```

## 4. Thực hành chính

### Bước 1: Vẽ flow text hiện tại

```text
Client
-> BasketService
-> OrderingService checkout
-> Ordering DB transaction
-> Outbox
-> Worker/Event
-> PaymentService/Webhook
-> Order status update
```

Điền theo repo thật, không đoán.

### Bước 2: Tạo Consistency Decision Table

```text
Step | Owner service | Consistency | Failure risk | Protection hiện có | Gap còn lại
-----|---------------|-------------|--------------|--------------------|------------
Validate basket | Basket/Ordering | Sync read / revalidate | Basket stale | ? | ?
Apply discount | Discount/Ordering | Sync check | Coupon expired/changed | ? | ?
Create order | Ordering | Local transaction | Order saved, event lost | Transactional outbox | ?
Publish order event | Ordering/Publisher | Eventual | Duplicate publish | Outbox/idempotency? | ?
Create/start payment | Payment/Future Saga | Eventual | Timeout/duplicate | ? | Idempotency key?
Receive webhook | Payment | Eventual | Duplicate/fake webhook | WebhookLogs + ProviderEventId | Signature/raw payload/publish event?
Mark order paid | Ordering | Eventual | Duplicate/out-of-order event | ? | Saga state?
Cancel/refund | Ordering/Payment | Eventual | Partial compensation fail | ? | Compensation trigger?
```

### Bước 3: Xác định missing states

```text
Order states hiện có:
- ?

Payment states hiện có:
- ?

States/gaps có thể cần:
- PendingPayment
- PaymentRequested
- Paid
- PaymentFailed
- Cancelled
- RefundRequired
- Refunded
- TimedOut
```

Không tự thêm hết. Chỉ ghi gap.

### Bước 4: Output bắt buộc

Cuối bài phải có:

```text
1. Consistency Decision Table đã điền theo repo thật.
2. Missing State/Gaps list cho Day 43-44.
```

Ví dụ gap hợp lệ sau Day 39:

```text
Gap 1: WebhookLogs đã có nhưng chưa verify signature/HMAC thật.
Gap 2: ProviderEventId đã có nhưng cần xác nhận unique constraint.
Gap 3: Chưa có PaymentSucceeded/PaymentFailed contract hoặc chưa publish.
Gap 4: Chưa có compensation trigger cho PaymentSucceeded sau OrderCancelled.
```

## 5. Failure drill

```text
Giả lập payment không hoàn tất hoặc webhook đến trễ.
```

Phân tích:

```text
Order đang ở trạng thái gì?
Payment đang ở trạng thái gì?
WebhookLog ghi gì?
Có event nội bộ nào được publish không?
Có compensation path không?
```

## 6. Câu hỏi interview

```text
1. Vì sao không dùng transaction xuyên Basket/Order/Payment?
2. Day 39 WebhookLogs giải quyết được gì và chưa giải quyết được gì?
3. Strong consistency cần ở đâu?
4. Eventual consistency chấp nhận ở đâu?
5. Outbox giải quyết lỗi gì?
6. Saga giải quyết lỗi gì?
7. Missing state nào khiến workflow khó production?
```

## 7. Kết luận

```text
Day 42 là bài ra quyết định consistency.
Không viết lại WebhookLog từ đầu.
Phải dựa vào Day 39: WebhookLogs + ProviderEventId đã có, rồi chỉ ra gap còn lại.
```

## 8. Optional commit

```text
Commit: Day 42: Distributed Consistency Decision Points
Tag: day-42-distributed-consistency-decision-points
```
