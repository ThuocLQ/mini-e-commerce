---
day: 42
title: "Distributed Consistency Decision Points"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
language: "vi"
style: "easy-production-learning"
---

# Day 42: Distributed Consistency Decision Points

## 0. Nói dễ hiểu: bài này học gì?

Khi checkout trong MicroShop, một request có thể đụng tới nhiều service:

```text
BasketService
DiscountService
OrderingService
PaymentService
Webhook
Worker
```

Vấn đề là: **mỗi service có database/trạng thái riêng**.

Không có chuyện một transaction SQL duy nhất rollback được mọi thứ ở tất cả service.

Bài này học cách trả lời câu hỏi:

```text
Ở bước nào cần đúng ngay?
Ở bước nào chấp nhận đúng sau vài giây?
Bước nào cần Outbox?
Bước nào cần Saga?
Bước nào cần Compensation?
Bước nào cần Idempotency?
```

Đây là bài “ra quyết định”, không phải bài code nặng.

---

## 1. Tình huống lỗi thực tế

Giả sử user checkout:

```text
1. Basket hợp lệ.
2. Order tạo thành công.
3. Payment chưa chắc thành công.
4. Webhook payment có thể đến muộn hoặc gửi duplicate.
```

Các lỗi có thể xảy ra:

```text
Order đã tạo nhưng event không publish được.
Payment thành công nhưng Order chưa cập nhật Paid.
Payment thất bại nhưng Order vẫn Pending.
Webhook gửi trùng 2 lần.
PaymentSucceeded đến sau khi Order đã Cancelled.
```

Nếu không thiết kế consistency rõ, hệ thống sẽ rơi vào trạng thái mơ hồ.

---

## 2. Khái niệm cần hiểu

### 2.1. Strong consistency

Dữ liệu đúng ngay lập tức trong cùng một boundary.

Ví dụ trong `OrderingService`:

```text
Create Order
Insert OutboxMessage
Commit cùng một DB transaction
```

Nếu transaction fail thì cả order và outbox đều fail.

### 2.2. Eventual consistency

Dữ liệu giữa nhiều service không đúng ngay lập tức, nhưng cuối cùng sẽ đồng bộ.

Ví dụ:

```text
OrderCreated được lưu.
Event publish sau.
NotificationWorker gửi thông báo sau.
OrderQueryService read model cập nhật sau.
```

### 2.3. Outbox

Outbox giải quyết lỗi:

```text
DB lưu thành công nhưng publish event fail.
```

Cách làm:

```text
Trong cùng transaction:
    save business data
    save outbox message

Background publisher:
    đọc outbox
    publish message
```

### 2.4. Saga

Saga dùng khi workflow trải qua nhiều service và nhiều bước.

Ví dụ:

```text
Order created
-> request payment
-> payment success
-> mark order paid
```

Nếu payment fail:

```text
cancel order
```

### 2.5. Compensation

Compensation không phải rollback DB.

Nó là hành động bù trừ nghiệp vụ.

Ví dụ:

```text
Payment succeeded nhưng order đã cancelled
-> cần refund hoặc đánh dấu RefundRequired
```

### 2.6. Idempotency

Idempotency nghĩa là xử lý lặp vẫn không làm sai dữ liệu.

Ví dụ:

```text
Webhook gửi 2 lần cùng ProviderEventId
-> chỉ xử lý 1 lần
```

---

## 3. Repo hiện tại cần kiểm tra

Chạy:

```powershell
Get-ChildItem Services/OrderingService -Recurse -Filter *.cs |
  Select-String -Pattern "Checkout|Order|OrderStatus|Outbox|Transaction|UnitOfWork|Payment"

Get-ChildItem Services/PaymentService -Recurse -Filter *.cs |
  Select-String -Pattern "Payment|PaymentStatus|Webhook|WebhookLog|ProviderEventId|Succeeded|Failed"

Get-ChildItem Services/BasketService -Recurse -Filter *.cs |
  Select-String -Pattern "Basket|Clear|Checkout|Redis"
```

Cần biết:

```text
Order hiện có status nào?
Payment hiện có status nào?
WebhookLogs hiện đã có field gì?
ProviderEventId đã dùng để chống duplicate chưa?
Checkout flow hiện dừng ở đâu?
```

Lưu ý theo tiến độ hiện tại:

```text
Day 39 đã có WebhookLogs cơ bản + ProviderEventId.
```

Vậy không được ghi là “chưa có WebhookLog”. Gap còn lại thường là:

```text
signature verification
raw payload hoặc payload hash
publish PaymentSucceeded/PaymentFailed event
compensation trigger
saga state rõ ràng
```

---

## 4. Lab chính: điền bảng quyết định consistency

Không cần tạo file docs mới. Điền bảng này ngay vào bài học/note.

```text
Step | Owner | Cần consistency kiểu gì? | Lỗi có thể xảy ra | Cách bảo vệ | Gap hiện tại
-----|-------|--------------------------|-------------------|-------------|-------------
Validate basket | Basket/Ordering | Sync check | Basket stale | Revalidate | ?
Apply discount | Discount/Ordering | Sync check | Coupon hết hạn | Recheck at checkout | ?
Create order | Ordering | Local transaction | Order saved, event lost | Transactional Outbox | ?
Publish event | Ordering Publisher | Eventual | Duplicate publish | Idempotent consumer | ?
Start payment | Payment/Saga | Eventual | Timeout/duplicate | Idempotency key | ?
Receive webhook | Payment | Eventual | Duplicate/fake webhook | WebhookLogs + ProviderEventId | signature?
Mark order paid | Ordering | Eventual | Duplicate/out-of-order | Saga state | ?
Cancel/refund | Ordering/Payment | Eventual | Partial failure | Compensation | ?
```

Điểm quan trọng: bảng này không cần đẹp. Nó cần đúng với repo.

---

## 5. Output bắt buộc của bài

Cuối bài phải có 2 thứ.

### 5.1. Consistency Decision Table đã điền

Ví dụ:

```text
Receive webhook:
- Owner: PaymentService
- Consistency: eventual
- Risk: duplicate/fake webhook
- Current protection: WebhookLogs + ProviderEventId
- Gap: chưa verify signature, chưa publish PaymentSucceeded event
```

### 5.2. Missing State/Gaps list

Ví dụ:

```text
Gap 1: Chưa có OrderPaymentSaga state.
Gap 2: Chưa rõ PaymentSucceeded/PaymentFailed contract.
Gap 3: WebhookLogs đã có nhưng thiếu signatureStatus/payloadHash.
Gap 4: Chưa có compensation rule cho PaymentSucceeded sau OrderCancelled.
```

---

## 6. Acceptance criteria cho Day 43

Day 42 phải chuẩn bị được đầu vào cho Day 43.

Day 43 nên xử lý được tối thiểu:

```text
PaymentSucceeded duplicate
-> Order chỉ Paid một lần.

PaymentFailed sau PaymentSucceeded
-> Không cancel Order đã Paid.

Payment timeout
-> Order không treo mãi ở trạng thái mơ hồ.
```

---

## 7. Câu hỏi interview

```text
1. Vì sao không dùng một transaction xuyên Basket/Order/Payment?
2. Strong consistency cần ở đâu?
3. Eventual consistency chấp nhận ở đâu?
4. Outbox giải quyết lỗi gì?
5. Saga giải quyết lỗi gì?
6. Compensation khác rollback DB thế nào?
7. Day 39 WebhookLogs đã giải quyết gì?
8. Còn thiếu gì để webhook production-safe hơn?
```

---

## 8. Kết luận

```text
Distributed consistency không phải học thuộc pattern.
Nó là việc nhìn từng bước workflow và quyết định:
- dùng transaction ở đâu
- dùng outbox ở đâu
- dùng saga ở đâu
- dùng idempotency ở đâu
- compensation ở đâu
```

Nếu chưa điền được bảng decision thì chưa nên nhảy sang code saga.
