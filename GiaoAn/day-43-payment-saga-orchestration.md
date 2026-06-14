---
day: 43
title: "Payment Saga Orchestration"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
style: "production-failure-scenario"
phase: "Stage 2 - Production Hardening"
---

# Day 43: Payment Saga Orchestration

## 0. Vấn đề production

Sau Day 42, ta biết checkout/payment cần workflow state rõ.

Vấn đề:

```text
Order tạo thành công.
Payment có thể success/fail/timeout.
Event/webhook có thể duplicate hoặc out-of-order.
Cần một nơi điều phối trạng thái payment flow.
```

Bài này là **design-first, code slice optional**.

Mục tiêu không phải build saga engine, mà là chốt payment saga slice đủ rõ để có thể implement an toàn.

Không làm:

```text
Không build generic saga framework.
Không tạo framework state machine phức tạp.
Không tạo service mới nếu chưa cần.
Không refactor toàn bộ checkout.
```

Repo reality cần xác nhận:

```powershell
Get-ChildItem BuildingBlocks.Contracts -Recurse -Filter *.cs |
  Select-String -Pattern "PaymentSucceeded|PaymentFailed|Payment|IntegrationEvent"
```

Nếu chưa có `PaymentSucceeded` / `PaymentFailed`:

```text
Đây là event contract sẽ tạo trong bài này nếu implement code slice.
Nếu không implement, ghi rõ là gap/target contract.
Không nói như repo đã có.
```

## 1. Kiến thức cần nắm

### 1.1. Saga

Saga quản lý workflow phân tán bằng nhiều local transaction.

Mỗi bước:

```text
Update local state.
Publish command/event.
Nếu bước sau fail thì chạy compensation.
```

### 1.2. Orchestration

Có một orchestrator/state owner điều phối flow.

Ví dụ:

```text
OrderPaymentSaga:
    OrderCreated
    -> PaymentRequested
    -> PaymentSucceeded
    -> OrderPaid

    PaymentFailed/Timeout
    -> OrderCancelled
```

Ưu điểm:

```text
Flow dễ nhìn.
Dễ debug.
Timeout rõ.
State rõ.
```

Nhược điểm:

```text
Orchestrator chứa logic workflow tập trung.
Cần quản lý state/idempotency cẩn thận.
```

### 1.3. Saga state tối thiểu

Không cần phức tạp.

Tối thiểu:

```text
SagaId
OrderId
PaymentId
State
StartedAtUtc
UpdatedAtUtc
TimeoutAtUtc
LastProcessedEventId
LastError
```

State gợi ý:

```text
Started
PaymentRequested
PaymentSucceeded
PaymentFailed
OrderPaid
OrderCancelled
TimedOut
```

### 1.4. Transition phải idempotent

Các event có thể đến lặp.

Rule:

```text
PaymentSucceeded đến 2 lần -> xử lý 1 lần.
PaymentFailed đến sau PaymentSucceeded -> không được cancel order đã paid nếu business không cho phép.
Timeout chạy sau success -> ignore hoặc log.
```

## 2. Repo cần nhìn vào đâu

```powershell
Get-ChildItem Services/OrderingService -Recurse -Filter *.cs |
  Select-String -Pattern "OrderStatus|Pending|Paid|Cancelled|Checkout|Outbox|Saga|Payment"

Get-ChildItem Services/PaymentService -Recurse -Filter *.cs |
  Select-String -Pattern "PaymentStatus|PaymentSucceeded|PaymentFailed|Webhook|Idempotency"

Get-ChildItem BuildingBlocks.Contracts -Recurse -Filter *.cs |
  Select-String -Pattern "OrderCreated|PaymentSucceeded|PaymentFailed|OrderCancelled|IntegrationEvent"
```

Cần xác nhận:

```text
Order có status gì?
Payment có status gì?
Có PaymentSucceeded/PaymentFailed event chưa?
Có saga state chưa?
Nếu chưa có, chỉ thiết kế/implement slice nhỏ.
```

## 3. Thực hành chính

### Bước 1: Chọn scope slice

Scope chuẩn của bài, nếu implement:

```text
OrderPaymentSaga cho một order.
Handle PaymentSucceeded.
Handle PaymentFailed.
Idempotency guard theo eventId hoặc state.
```

Không làm quá scope.

### Bước 2: Thiết kế state transition

```text
PaymentRequested
    + PaymentSucceeded
        -> OrderPaid

PaymentRequested
    + PaymentFailed
        -> OrderCancelled

PaymentSucceeded/OrderPaid
    + PaymentSucceeded duplicate
        -> ignore

OrderPaid
    + PaymentFailed late
        -> ignore/conflict log, không cancel order

PaymentRequested
    + Timeout
        -> TimedOut -> OrderCancelled
```

### Bước 3: Thiết kế model/table nếu chưa có

Pseudo:

```text
order_payment_sagas
- saga_id
- order_id
- payment_id
- state
- started_at_utc
- updated_at_utc
- timeout_at_utc
- last_processed_event_id
- last_error
```

### Bước 4: Handler pseudo-code

```text
Handle(PaymentSucceeded event):
    saga = find by orderId/paymentId
    if saga already OrderPaid:
        return

    if saga state is OrderCancelled/TimedOut:
        mark compensation required or log conflict
        return

    mark saga OrderPaid
    mark order Paid
    save lastProcessedEventId

Handle(PaymentFailed event):
    saga = find by orderId/paymentId
    if saga already OrderPaid:
        log late failure/conflict
        return

    if saga already OrderCancelled:
        return

    mark saga OrderCancelled
    mark order Cancelled
    save lastProcessedEventId
```

### Bước 5: Code slice optional

Nếu implement, acceptable output:

```text
- Saga state model/repository tối thiểu
- PaymentSucceeded handler
- PaymentFailed handler
- Duplicate guard
- Unit/integration test đơn giản nếu đủ thời gian
```

Nếu không implement code trong buổi này, acceptable output là:

```text
- Saga transition table.
- Event contract gap list.
- Acceptance criteria cho PaymentSucceeded/PaymentFailed handlers.
```

Không acceptable:

```text
- Build generic saga framework.
- Thêm nhiều service mới.
- Refactor toàn bộ Ordering/Payment.
```

## 4. Failure drill

Drill 1:

```text
Gửi PaymentSucceeded 2 lần.
```

Kỳ vọng:

```text
Order chỉ Paid một lần.
Saga không tạo side effect duplicate.
```

Drill 2:

```text
Gửi PaymentFailed sau PaymentSucceeded.
```

Kỳ vọng:

```text
Không cancel order đã paid.
Có conflict log hoặc compensation-required note.
```

Drill 3:

```text
Payment timeout.
```

Kỳ vọng:

```text
Saga chuyển TimedOut/OrderCancelled nếu chưa success.
Nếu success đến sau timeout, cần compensation/refund decision.
```

## 5. Câu hỏi interview

```text
1. Saga khác distributed transaction thế nào?
2. Orchestration khác choreography thế nào?
3. Vì sao saga cần state?
4. Vì sao không build generic saga framework ngay?
5. PaymentSucceeded duplicate xử lý sao?
6. PaymentFailed late xử lý sao?
7. Timeout trong saga để làm gì?
8. Compensation trong payment flow là gì?
```

## 6. Kết luận

```text
Saga orchestration tốt khi workflow cần state và timeout rõ.
Bài này chỉ cần payment saga slice nhỏ, không over-engineer.
Điểm quan trọng: transition + idempotency + failure path.
```

## 7. Optional commit

```text
Commit: Day 43: Payment Saga Orchestration
Tag: day-43-payment-saga-orchestration
```
