---
day: 43
title: "Payment Saga Orchestration"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
language: "vi"
style: "easy-production-learning"
---

# Day 43: Payment Saga Orchestration

## 0. Nói dễ hiểu: bài này học gì?

Day 42 đã chỉ ra: checkout/payment không thể dựa vào một transaction lớn.

Day 43 học cách quản lý workflow payment bằng **Saga Orchestration**.

Nói đơn giản:

```text
Ta cần một nơi giữ trạng thái:
Order này đang chờ payment?
Payment thành công chưa?
Payment fail chưa?
Có timeout chưa?
Có event duplicate không?
```

Bài này là **design-first**.

Code slice là optional.

Không làm:

```text
Không build generic saga framework.
Không tạo service mới nếu chưa cần.
Không refactor toàn bộ checkout.
Không xử lý webhook signature.
Không xử lý refund thật.
```

---

## 1. Tình huống lỗi thực tế

User checkout xong:

```text
OrderCreated
PaymentRequested
```

Sau đó có thể xảy ra:

```text
PaymentSucceeded đến 1 lần.
PaymentSucceeded đến 2 lần.
PaymentFailed đến.
PaymentFailed đến sau PaymentSucceeded.
Không có webhook nào đến, bị timeout.
```

Nếu không có saga state, hệ thống khó biết order đang ở đâu.

---

## 2. Saga là gì?

Saga là cách quản lý một workflow nhiều bước qua nhiều service.

Thay vì transaction xuyên service, ta dùng nhiều local transaction + trạng thái workflow.

Ví dụ:

```text
OrderPaymentSaga
    PaymentRequested
    -> PaymentSucceeded
    -> OrderPaid
```

Nếu fail:

```text
OrderPaymentSaga
    PaymentRequested
    -> PaymentFailed
    -> OrderCancelled
```

Nếu timeout:

```text
OrderPaymentSaga
    PaymentRequested
    -> TimedOut
    -> OrderCancelled
```

---

## 3. Orchestration là gì?

Orchestration nghĩa là có một nơi điều phối flow.

Ví dụ:

```text
OrderPaymentSaga quyết định:
- PaymentSucceeded thì mark order Paid
- PaymentFailed thì cancel order
- Timeout thì cancel hoặc mark TimedOut
```

Ưu điểm:

```text
Flow dễ nhìn.
Debug dễ hơn.
Timeout rõ hơn.
State rõ hơn.
```

Nhược điểm:

```text
Orchestrator chứa logic workflow tập trung.
Phải cẩn thận idempotency.
```

---

## 4. Saga nên đặt ở đâu?

Có 2 option.

### Option A: Đặt trong OrderingService

Phù hợp khi:

```text
Saga chủ yếu điều phối trạng thái Order.
Project đang học/training-stage.
Chưa cần tách thêm service.
```

### Option B: Tạo worker/service riêng

Phù hợp khi:

```text
Workflow phức tạp.
Nhiều service tham gia.
Cần scale/operate riêng.
```

Khuyến nghị hiện tại:

```text
Bắt đầu bằng OrderingService hoặc worker nhỏ cùng bounded context Ordering.
Chưa tạo service mới nếu chưa có lý do rõ.
```

---

## 5. Repo cần kiểm tra

Kiểm tra event contract:

```powershell
Get-ChildItem BuildingBlocks.Contracts -Recurse -Filter *.cs |
  Select-String -Pattern "PaymentSucceeded|PaymentFailed|Payment|IntegrationEvent"
```

Nếu chưa có:

```text
PaymentSucceeded/PaymentFailed là contract sẽ tạo nếu implement code slice.
Nếu chưa implement, ghi là gap/target contract.
```

Kiểm tra order/payment state:

```powershell
Get-ChildItem Services/OrderingService -Recurse -Filter *.cs |
  Select-String -Pattern "OrderStatus|Pending|Paid|Cancelled|Checkout|Outbox|Saga|Payment"

Get-ChildItem Services/PaymentService -Recurse -Filter *.cs |
  Select-String -Pattern "PaymentStatus|Webhook|ProviderEventId|Succeeded|Failed"
```

---

## 6. Thiết kế saga state

Tối thiểu cần:

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
PaymentRequested
OrderPaid
OrderCancelled
TimedOut
CompensationRequired
```

Không cần tạo quá nhiều state nếu chưa dùng.

---

## 7. Transition table

Đây là phần quan trọng nhất của bài.

```text
Current state | Incoming event | Next state | Action
--------------|----------------|------------|-------
PaymentRequested | PaymentSucceeded | OrderPaid | mark order Paid
PaymentRequested | PaymentFailed | OrderCancelled | cancel order
PaymentRequested | Timeout | TimedOut/OrderCancelled | cancel or timeout order
OrderPaid | PaymentSucceeded duplicate | OrderPaid | ignore
OrderPaid | PaymentFailed late | OrderPaid | log conflict, do not cancel
OrderCancelled | PaymentSucceeded late | CompensationRequired | refund/manual intervention
```

Nếu không có bảng này thì code saga rất dễ sai.

---

## 8. Handler pseudo-code

### PaymentSucceeded

```text
Handle PaymentSucceeded:
    if eventId already processed:
        return

    saga = find by orderId/paymentId

    if saga is OrderPaid:
        mark event processed
        return

    if saga is OrderCancelled or TimedOut:
        mark CompensationRequired
        mark event processed
        return

    mark saga OrderPaid
    mark order Paid
    mark event processed
```

### PaymentFailed

```text
Handle PaymentFailed:
    if eventId already processed:
        return

    saga = find by orderId/paymentId

    if saga is OrderPaid:
        log late failure
        mark event processed
        return

    mark saga OrderCancelled
    mark order Cancelled
    mark event processed
```

---

## 9. Code slice optional

Nếu implement, chỉ làm mỏng:

```text
1. Tạo PaymentSucceeded/PaymentFailed contract nếu chưa có.
2. Tạo OrderPaymentSaga model/table tối thiểu.
3. Tạo handler PaymentSucceeded.
4. Tạo handler PaymentFailed.
5. Thêm duplicate guard theo eventId hoặc state.
```

Không acceptable:

```text
Generic saga framework.
Nhiều service mới.
Refactor toàn bộ payment flow.
Refund thật.
Webhook signature.
```

Các phần đó để Day 44 hoặc sau.

---

## 10. Failure drill

### Drill 1: PaymentSucceeded duplicate

```text
Gửi PaymentSucceeded 2 lần.
```

Kỳ vọng:

```text
Order chỉ Paid một lần.
Saga không tạo side effect duplicate.
```

### Drill 2: PaymentFailed sau PaymentSucceeded

```text
Gửi PaymentSucceeded trước.
Sau đó gửi PaymentFailed.
```

Kỳ vọng:

```text
Không cancel Order đã Paid.
Có log conflict hoặc compensation note.
```

### Drill 3: Timeout

```text
Không gửi webhook payment.
Để saga quá TimeoutAtUtc.
```

Kỳ vọng:

```text
Saga không treo mãi.
Order chuyển TimedOut/Cancelled hoặc có state rõ.
```

---

## 11. Câu hỏi interview

```text
1. Saga khác distributed transaction thế nào?
2. Orchestration khác choreography thế nào?
3. Vì sao saga cần state?
4. Vì sao không build generic saga framework ngay?
5. PaymentSucceeded duplicate xử lý sao?
6. PaymentFailed late xử lý sao?
7. Timeout trong saga dùng để làm gì?
8. Saga nên đặt ở OrderingService hay service riêng?
```

---

## 12. Kết luận

```text
Saga không phải code pattern để trang trí.
Saga là cách giữ workflow không rơi vào trạng thái mơ hồ.

Day 43 chỉ cần:
- state rõ
- transition rõ
- duplicate handling rõ
- failure path rõ
```
