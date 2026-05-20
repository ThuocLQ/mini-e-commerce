---
checkpoint: 4
title: "Ordering + Checkout + Discount + Payment/Webhook Review"
duration: "90–120 phút"
phase: "Phase 1.4 - Ordering + Checkout + Payment/Webhook Intro"
project: "MicroShop"
testing: "Postman-first"
type: "checkpoint"
---

# Checkpoint 4: Ordering + Checkout + Discount + Payment/Webhook Review

## 1. Mục tiêu checkpoint

Checkpoint này dùng để khóa lại Phase 1.4 trước khi sang RabbitMQ + Reliability.

Phase vừa học gồm:

```text
Buổi 15: OrderingService
Buổi 16: Checkout Flow: Basket → Order
Buổi 17: DiscountService + Strategy Intro
Buổi 18: PaymentService + Payment Webhook Intro
```

Mục tiêu checkpoint không phải học thêm service mới, mà là:

```text
[ ] Review lại boundary của OrderingService, DiscountService, PaymentService.
[ ] Chạy regression test bằng Postman.
[ ] Kiểm tra flow checkout/payment baseline.
[ ] Ghi lại production risk: distributed consistency, idempotency, duplicate webhook, retry.
[ ] Viết demo script để sau này show project.
[ ] Tạo interview note ngắn cho phase này.
[ ] Đủ điều kiện sang Phase 1.5: RabbitMQ + Reliability.
```

Câu nhớ:

```text
Checkpoint không phải nghỉ giữa chặng.
Checkpoint là lúc biến kiến thức đã học thành năng lực có thể demo, debug và giải thích.
```

---

## 2. Phase 1.4 đã build được gì?

Sau Phase 1.4, MicroShop đã có thêm các mảnh quan trọng của e-commerce flow:

```text
BasketService
→ OrderingService
→ DiscountService
→ PaymentService
→ Payment webhook demo
```

Tổng quan service:

| Service | Vai trò chính |
| --- | --- |
| `BasketService` | Giữ giỏ hàng của user |
| `OrderingService` | Tạo order, lưu order/order items |
| `DiscountService` | Kiểm tra coupon và tính discount |
| `PaymentService` | Tạo payment và nhận webhook payment |
| `IdentityService` | Cấp JWT cho security flow |
| `CatalogService` | Quản lý sản phẩm/catalog |

Boundary cần nhớ:

```text
OrderingService không nên tự tính coupon phức tạp.
DiscountService không tạo order.
PaymentService không quản lý basket.
BasketService không ghi order.
```

---

## 3. Kiến trúc hiện tại của Phase 1.4

### 3.1. OrderingService

Đã học ở Buổi 15:

```text
POST /orders
GET /orders
GET /orders/{id}
```

Nhiệm vụ:

```text
Tạo order.
Lưu order và order items.
Trả OrderDto.
```

Keyword cần nhớ:

```text
Order
OrderItem
OrderStatus
OrderDto
CreateOrderCommand
IOrderRepository
DapperOrderRepository
```

### 3.2. Checkout Flow

Đã học ở Buổi 16:

```text
POST /checkout
```

Flow baseline:

```text
Client/Postman
→ POST /checkout
→ OrderingService
→ IBasketClient.GetBasketAsync(customerId)
→ tạo Order
→ IOrderRepository.CreateAsync(order)
→ IBasketClient.ClearBasketAsync(customerId)
→ trả OrderDto
```

Điểm cần nhớ:

```text
OrderingService gọi BasketService qua abstraction IBasketClient.
OrderingService không đọc Redis/database của BasketService trực tiếp.
```

Production risk đã bắt đầu thấy:

```text
Order tạo thành công nhưng clear basket fail.
BasketService down thì checkout fail.
Nếu retry checkout không cẩn thận có thể tạo order trùng.
```

### 3.3. DiscountService

Đã học ở Buổi 17:

```text
GET /discounts/{code}
POST /discounts/apply
```

Flow apply coupon:

```text
POST /discounts/apply
→ ApplyDiscountCommand
→ ApplyDiscountHandler
→ IDiscountRepository
→ Coupon
→ DiscountStrategyFactory
→ IDiscountStrategy
→ DiscountResultDto
```

Keyword cần nhớ:

```text
Coupon
DiscountType
Percentage
FixedAmount
StrategyPattern
IDiscountStrategy
DiscountStrategyFactory
DiscountAmount
FinalAmount
IsValid
```

Điểm thiết kế quan trọng:

```text
DiscountService không chỉ là CRUD coupon.
DiscountService là nơi đóng gói rule tính giảm giá.
```

### 3.4. PaymentService + Webhook

Đã học ở Buổi 18:

```text
POST /payments
GET /payments/{id}
POST /webhooks/payment
```

Flow payment baseline:

```text
POST /payments
→ Payment Pending
→ POST /webhooks/payment
→ Payment Succeeded hoặc Failed
```

Keyword cần nhớ:

```text
Payment
PaymentStatus
Pending
Succeeded
Failed
Webhook
ProviderTransactionId
PaymentWebhookCommand
Idempotency
WebhookLog
```

Câu nhớ:

```text
Webhook là HTTP callback từ hệ thống bên ngoài gọi ngược vào hệ thống của mình.
```

---

## 4. Sơ đồ phase hiện tại

Sơ đồ logical hiện tại:

```text
Client/Postman
    |
    | login
    v
IdentityService
    |
    | JWT
    v

Client/Postman
    |
    | add/view basket
    v
BasketService
    |
    | checkout
    v
OrderingService
    |
    | optional apply coupon
    v
DiscountService
    |
    | create payment
    v
PaymentService
    ^
    |
    | webhook callback
    |
Payment Provider giả lập/Postman
```

Sơ đồ trách nhiệm:

```text
BasketService     = giỏ hàng
OrderingService   = đơn hàng
DiscountService   = mã giảm giá
PaymentService    = thanh toán
Webhook endpoint  = cổng nhận kết quả từ bên ngoài
```

---

## 5. Regression checklist trước khi test

Trước khi chạy Postman, kiểm tra các service cần chạy:

```text
[ ] IdentityService chạy được.
[ ] CatalogService chạy được nếu checkout/basket cần product.
[ ] BasketService chạy được.
[ ] OrderingService chạy được.
[ ] DiscountService chạy được.
[ ] PaymentService chạy được.
```

Port gợi ý:

| Service | URL |
| --- | --- |
| ApiGateway | `http://localhost:5000` |
| CatalogService | `http://localhost:5001` |
| BasketService | `http://localhost:5002` |
| IdentityService | `http://localhost:5003` |
| OrderingService | `http://localhost:5004` |
| DiscountService | `http://localhost:5005` |
| PaymentService | `http://localhost:5006` |

Lệnh chạy từng service:

```bash
dotnet run --project Services/IdentityService/IdentityService.csproj
dotnet run --project Services/CatalogService/CatalogService.csproj
dotnet run --project Services/BasketService/BasketService.csproj
dotnet run --project Services/OrderingService/OrderingService.csproj
dotnet run --project Services/DiscountService/DiscountService.csproj
dotnet run --project Services/PaymentService/PaymentService.csproj
```

Build nhanh:

```bash
dotnet build
```

---

## 6. Postman Environment

Tạo hoặc cập nhật environment:

```text
MicroShop Local
```

Variables:

| Variable | Initial value |
| --- | --- |
| `identity_url` | `http://localhost:5003` |
| `catalog_url` | `http://localhost:5001` |
| `basket_url` | `http://localhost:5002` |
| `ordering_url` | `http://localhost:5004` |
| `discount_url` | `http://localhost:5005` |
| `payment_url` | `http://localhost:5006` |
| `admin_token` | để trống |
| `customer_id` | `11111111-1111-1111-1111-111111111111` |
| `order_id` | để trống |
| `payment_id` | để trống |
| `coupon_code` | `SAVE10` |

Gợi ý collection:

```text
MicroShop - Checkpoint 4
```

---

## 7. Postman Regression Test

### Test 1: Login Admin

Request:

```text
POST {{identity_url}}/auth/login
```

Body:

```json
{
  "userName": "admin",
  "password": "Admin@123"
}
```

Expected:

```text
HTTP 200 OK
Có accessToken
```

Postman Tests:

```javascript
const json = pm.response.json();
pm.environment.set("admin_token", json.accessToken);
```

Pass khi:

```text
[ ] admin_token được lưu vào environment.
```

---

### Test 2: Catalog write endpoint được bảo vệ

Request không token:

```text
POST {{catalog_url}}/products
```

Body:

```json
{
  "name": "Checkpoint Product",
  "description": "Product for checkpoint 4",
  "price": 999
}
```

Expected:

```text
HTTP 401 Unauthorized
```

Request có admin token:

```text
POST {{catalog_url}}/products
Authorization: Bearer {{admin_token}}
```

Body:

```json
{
  "name": "Checkpoint Product",
  "description": "Product for checkpoint 4",
  "price": 999
}
```

Expected:

```text
HTTP 201 Created
```

Pass khi:

```text
[ ] No token trả 401.
[ ] Admin token tạo product thành công.
```

---

### Test 3: DiscountService apply coupon

Request:

```text
POST {{discount_url}}/discounts/apply
```

Body:

```json
{
  "couponCode": "SAVE10",
  "orderAmount": 2197
}
```

Expected:

```text
HTTP 200 OK
isValid = true
discountAmount = 219.7
finalAmount = 1977.3
```

Test invalid coupon:

```json
{
  "couponCode": "NOTFOUND",
  "orderAmount": 2197
}
```

Expected:

```text
HTTP 200 OK
isValid = false
discountAmount = 0
finalAmount = 2197
```

Pass khi:

```text
[ ] SAVE10 tính đúng.
[ ] NOTFOUND invalid.
[ ] EXPIRED20/DISABLED15 invalid nếu có test.
```

---

### Test 4: Checkout flow Basket → Order

Flow tùy code thực tế của BasketService hiện tại. Mục tiêu là có basket trước rồi checkout.

Checklist cần đạt:

```text
[ ] Tạo hoặc cập nhật basket cho customer.
[ ] Gọi POST /checkout.
[ ] OrderingService gọi BasketService lấy basket.
[ ] OrderingService tạo order.
[ ] Basket được clear sau checkout nếu code đã làm.
[ ] Response trả OrderDto.
```

Expected ở mức business:

```text
Order được tạo từ basket items.
Order có customerId đúng.
Order items khớp basket items.
TotalAmount hợp lý.
```

Nếu chưa có endpoint seed basket thuận tiện, có thể ghi lại:

```text
BasketService chưa có API seed/test đủ tiện, cần bổ sung ở bài sau hoặc checklist cleanup.
```

Không tự phá scope checkpoint để refactor BasketService quá sâu.

---

### Test 5: Create Payment

Request:

```text
POST {{payment_url}}/payments
```

Body:

```json
{
  "orderId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "customerId": "11111111-1111-1111-1111-111111111111",
  "amount": 1977.3,
  "currency": "VND"
}
```

Expected:

```text
HTTP 201 Created
status = Pending
```

Postman Tests:

```javascript
const json = pm.response.json();
pm.environment.set("payment_id", json.id);
```

Pass khi:

```text
[ ] payment_id được lưu.
[ ] status = Pending.
```

Nếu đã có `order_id` từ checkout, dùng `{{order_id}}` thay vì hardcode.

---

### Test 6: Payment Webhook Success

Request:

```text
POST {{payment_url}}/webhooks/payment
```

Body:

```json
{
  "paymentId": "{{payment_id}}",
  "providerTransactionId": "txn_checkpoint_001",
  "status": "SUCCEEDED",
  "failureReason": null
}
```

Expected:

```text
HTTP 200 OK
status = Succeeded
providerTransactionId = txn_checkpoint_001
completedAtUtc có giá trị
```

Pass khi:

```text
[ ] Webhook cập nhật payment status thành Succeeded.
```

---

### Test 7: Get Payment After Webhook

Request:

```text
GET {{payment_url}}/payments/{{payment_id}}
```

Expected:

```text
HTTP 200 OK
status = Succeeded
providerTransactionId = txn_checkpoint_001
```

Pass khi:

```text
[ ] GET payment reflect đúng trạng thái sau webhook.
```

---

### Test 8: Webhook Not Found

Request:

```text
POST {{payment_url}}/webhooks/payment
```

Body:

```json
{
  "paymentId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  "providerTransactionId": "txn_not_found",
  "status": "SUCCEEDED",
  "failureReason": null
}
```

Expected:

```text
HTTP 404 Not Found
```

Pass khi:

```text
[ ] Webhook payment not found được xử lý rõ ràng.
```

---

## 8. Phase 1.4 checklist

### OrderingService

```text
[ ] Có Order domain model.
[ ] Có OrderItem domain model.
[ ] Có OrderStatus.
[ ] Có OrderDto.
[ ] Có IOrderRepository.
[ ] Có DapperOrderRepository hoặc repository implementation tương ứng.
[ ] POST /orders chạy được.
[ ] GET /orders chạy được.
[ ] GET /orders/{id} chạy được.
```

### Checkout Flow

```text
[ ] Có IBasketClient.
[ ] Có HttpBasketClient hoặc implementation tương ứng.
[ ] Có CheckoutCommand.
[ ] Có CheckoutHandler.
[ ] Có CheckoutEndpoint.
[ ] POST /checkout chạy được.
[ ] Checkout không đọc trực tiếp Redis của BasketService.
[ ] Checkout có xử lý basket rỗng/not found ở mức baseline.
```

### DiscountService

```text
[ ] Có Coupon domain model.
[ ] Có DiscountType.
[ ] Có IDiscountStrategy.
[ ] Có PercentageDiscountStrategy.
[ ] Có FixedAmountDiscountStrategy.
[ ] Có DiscountStrategyFactory.
[ ] Có IDiscountRepository.
[ ] Có InMemoryDiscountRepository.
[ ] GET /discounts/{code} chạy được.
[ ] POST /discounts/apply chạy được.
```

### PaymentService

```text
[ ] Có Payment domain model.
[ ] Có PaymentStatus: Pending/Succeeded/Failed.
[ ] Có IPaymentRepository.
[ ] Có InMemoryPaymentRepository.
[ ] Có CreatePaymentCommand/Handler.
[ ] Có GetPaymentByIdQuery/Handler.
[ ] Có PaymentWebhookCommand/Handler.
[ ] POST /payments chạy được.
[ ] GET /payments/{id} chạy được.
[ ] POST /webhooks/payment chạy được.
```

### Postman

```text
[ ] Có environment MicroShop Local.
[ ] Có collection MicroShop - Checkpoint 4.
[ ] Có request login.
[ ] Có request discount apply.
[ ] Có request checkout.
[ ] Có request create payment.
[ ] Có request payment webhook.
[ ] Có request get payment.
```

---

## 9. Refactor/Cleanup nhẹ

Checkpoint chỉ cleanup nhẹ, không làm lại architecture lớn.

Việc nên làm:

```text
[ ] Kiểm tra tên service/folder dùng đúng: CatalogService, BasketService, OrderingService, DiscountService, PaymentService.
[ ] Kiểm tra không còn ProductService.
[ ] Kiểm tra Program.cs của service mới không quá dài.
[ ] Endpoint không chứa business rule nặng.
[ ] Handler không gọi concrete repository trực tiếp.
[ ] Domain không using Infrastructure/Dapper/Redis.
[ ] appsettings.Development.json có port/config rõ ràng.
[ ] Postman environment không hardcode lung tung quá nhiều.
```

Việc không nên làm trong checkpoint:

```text
[ ] Không đổi toàn bộ project sang multi-project architecture.
[ ] Không thêm RabbitMQ trước bài 19.
[ ] Không thêm Outbox/Idempotency full vào PaymentService.
[ ] Không tích hợp payment provider thật.
[ ] Không refactor quá sâu BasketService nếu chưa cần.
[ ] Không đổi roadmap bài 19.
```

---

## 10. Production risk cần hiểu sau phase này

### Risk 1: Checkout tạo order thành công nhưng clear basket fail

Vấn đề:

```text
Order đã tạo.
Basket vẫn còn.
User retry checkout có thể tạo order trùng.
```

Cách xử lý production sau này:

```text
Idempotency key.
Outbox/event.
Saga/process manager.
Retry có kiểm soát.
```

### Risk 2: BasketService down khi checkout

Vấn đề:

```text
OrderingService gọi BasketService sync.
BasketService down thì checkout fail.
```

Cách xử lý sau này:

```text
Timeout.
Retry với backoff.
Circuit breaker.
Fallback hoặc trả lỗi rõ ràng.
Observability để trace lỗi downstream.
```

### Risk 3: Discount thay đổi giữa checkout và payment

Vấn đề:

```text
Coupon SAVE10 hợp lệ lúc apply.
Nhưng trước khi payment, coupon bị disable/expired.
```

Câu hỏi thiết kế:

```text
Order nên lưu discount snapshot hay gọi DiscountService lại?
```

Gợi ý production:

```text
Order nên lưu discountAmount/finalAmount đã được xác nhận tại thời điểm checkout.
Cần audit rule áp dụng.
```

### Risk 4: Webhook bị gửi nhiều lần

Vấn đề:

```text
Provider retry cùng webhook.
Nếu xử lý không idempotent, hệ thống có thể update/publish/gửi email nhiều lần.
```

Cách xử lý sau này:

```text
Webhook event id.
Inbox/WebhookLog.
Idempotency.
Unique constraint.
```

### Risk 5: Payment succeeded nhưng Order chưa cập nhật

Vấn đề:

```text
PaymentService biết payment success.
OrderingService chưa biết order đã paid.
```

Cách xử lý sau này:

```text
Publish PaymentSucceeded event.
OrderingService consume event để update Order status.
Outbox đảm bảo event không mất.
Saga quản lý workflow dài hơn.
```

---

## 11. ADR / Technical note cần viết

Tạo file:

```text
docs/adr/0004-ordering-discount-payment-boundary.md
```

Nội dung gợi ý:

```markdown
# ADR 0004: Ordering, Discount and Payment Service Boundary

## Status

Accepted

## Context

MicroShop now has OrderingService, DiscountService and PaymentService. Checkout requires basket data, discount calculation and later payment handling. If these responsibilities are mixed in one service, the code becomes harder to change and production risks become unclear.

## Decision

We keep the boundaries as follows:

- OrderingService owns orders and order items.
- DiscountService owns coupon validation and discount calculation.
- PaymentService owns payment records and payment webhook handling.
- BasketService owns basket data.

OrderingService may call BasketService during checkout. Discount and Payment integration will be introduced gradually, but each service keeps its own responsibility.

## Consequences

- Checkout flow is easier to understand.
- Payment and discount rules do not pollute OrderingService.
- Future event-driven integration can publish PaymentSucceeded, PaymentFailed or OrderCreated events.
- We still need idempotency, outbox, webhook log and saga in later phases.
```

Checkpoint pass nếu:

```text
[ ] ADR được tạo hoặc note tương đương được ghi vào docs.
```

---

## 12. Demo script cho portfolio

Viết file hoặc note:

```text
docs/demo/checkpoint-4-demo-script.md
```

Script demo 3–5 phút:

```text
1. Mở solution MicroShop.
2. Chạy các service cần thiết.
3. Login admin để lấy JWT.
4. Gọi Catalog write endpoint với Admin token để chứng minh security baseline.
5. Apply coupon SAVE10 qua DiscountService.
6. Checkout basket sang order qua OrderingService.
7. Tạo payment Pending qua PaymentService.
8. Gửi webhook success.
9. GET payment để thấy status Succeeded.
10. Giải thích risk production: webhook duplicate, idempotency, outbox, saga.
```

Câu nói demo mẫu:

```text
Ở phase này, tôi đã build được phần core e-commerce flow gồm Ordering, Checkout, Discount và Payment baseline.
Tôi tách DiscountService để rule giảm giá không bị nhồi vào checkout.
PaymentService có webhook endpoint để mô phỏng provider callback.
Hiện tại flow vẫn là baseline, các vấn đề production như duplicate webhook, outbox, idempotency và saga sẽ được xử lý ở các phase sau.
```

---

## 13. Interview notes

Tạo file hoặc ghi vào Notion:

```text
docs/interview/checkpoint-4-interview-notes.md
```

Câu hỏi cần trả lời được:

### Câu 1: Vì sao tách DiscountService khỏi OrderingService?

Đáp án gợi ý:

```text
Vì discount rule có thể phát triển độc lập với order.
OrderingService nên tập trung vào order lifecycle.
DiscountService quản lý coupon, discount type và cách tính giảm giá.
Tách boundary giúp tránh CheckoutHandler bị nhồi quá nhiều if/else và rule tiền bạc.
```

### Câu 2: Webhook khác RabbitMQ/Kafka thế nào?

Đáp án gợi ý:

```text
Webhook là HTTP callback từ hệ thống bên ngoài gọi vào hệ thống của mình.
RabbitMQ/Kafka là messaging infrastructure dùng cho event/message nội bộ hoặc hệ thống phân tán.
Webhook vẫn là HTTP, cần endpoint public/secured, signature verification và idempotency.
```

### Câu 3: Vì sao payment webhook cần idempotency?

Đáp án gợi ý:

```text
Payment provider có thể retry cùng một webhook nhiều lần khi timeout hoặc không nhận được response.
Nếu không idempotent, hệ thống có thể xử lý payment nhiều lần, publish event trùng hoặc gửi notification trùng.
```

### Câu 4: Nếu payment success nhưng update order fail thì sao?

Đáp án gợi ý:

```text
Đây là distributed consistency problem.
Không thể dùng transaction local đơn giản giữa PaymentService và OrderingService.
Sau này cần event-driven flow, outbox, idempotent consumer và có thể dùng Saga để đảm bảo workflow có retry/compensation rõ ràng.
```

### Câu 5: Vì sao checkout hiện tại dùng REST gọi BasketService vẫn được?

Đáp án gợi ý:

```text
Ở foundation phase, REST giúp flow dễ hiểu và dễ test.
Nhưng đây chưa phải production final.
Sau này cần timeout/retry/circuit breaker/idempotency hoặc chuyển một phần sang event-driven tùy use case.
```

---

## 14. Flashcard keyword cho checkpoint

Gợi ý thêm vào flashcard builder:

```json
[
  {
    "front": "Checkpoint",
    "back": "Điểm dừng để review, test, cleanup, docs và chuẩn bị phase tiếp theo.",
    "lesson": "Checkpoint4",
    "tag": "Learning"
  },
  {
    "front": "ServiceBoundary",
    "back": "Ranh giới trách nhiệm của từng service.",
    "lesson": "Checkpoint4",
    "tag": "Architecture"
  },
  {
    "front": "DistributedConsistency",
    "back": "Vấn đề nhất quán dữ liệu giữa nhiều service/database.",
    "lesson": "Checkpoint4",
    "tag": "Consistency"
  },
  {
    "front": "DuplicateWebhook",
    "back": "Cùng một webhook có thể được provider gửi nhiều lần.",
    "lesson": "Checkpoint4",
    "tag": "Webhook"
  },
  {
    "front": "PaymentSucceeded",
    "back": "Event/trạng thái cho biết payment đã thành công.",
    "lesson": "Checkpoint4",
    "tag": "Payment"
  },
  {
    "front": "OrderPaid",
    "back": "Trạng thái order sau khi payment thành công.",
    "lesson": "Checkpoint4",
    "tag": "Ordering"
  },
  {
    "front": "IdempotentWebhook",
    "back": "Webhook xử lý lặp lại vẫn cho cùng kết quả, không tạo side effect sai.",
    "lesson": "Checkpoint4",
    "tag": "Reliability"
  },
  {
    "front": "DemoScript",
    "back": "Kịch bản demo ngắn để show project và giải thích kiến trúc.",
    "lesson": "Checkpoint4",
    "tag": "Portfolio"
  }
]
```

---

## 15. Điều kiện pass checkpoint

Bạn pass Checkpoint 4 khi:

```text
[ ] Các service Phase 1.4 build được.
[ ] OrderingService tạo/xem order được.
[ ] Checkout flow chạy được hoặc có note rõ phần chưa chạy do thiếu seed/test basket.
[ ] DiscountService apply coupon chạy được.
[ ] PaymentService tạo payment Pending được.
[ ] Payment webhook cập nhật Succeeded/Failed được.
[ ] Postman collection Checkpoint 4 có đủ request chính.
[ ] Có ADR hoặc technical note về Order/Discount/Payment boundary.
[ ] Có demo script 3–5 phút.
[ ] Trả lời được các câu interview notes.
[ ] Ghi lại ít nhất 3 production risk của phase này.
```

Nếu chưa pass hết, không sao. Ghi rõ:

```text
Known gaps:
- ...
Next fix:
- ...
```

Checkpoint quan trọng hơn việc làm “cho có”.

---

## 16. Known gaps nên chấp nhận ở thời điểm này

Các gap sau được phép tồn tại sau Checkpoint 4:

```text
[ ] Chưa có InventoryService.
[ ] Chưa có ShippingService.
[ ] Chưa tích hợp PaymentService full vào OrderingService.
[ ] Chưa update Order status sau PaymentSucceeded.
[ ] Chưa có Outbox.
[ ] Chưa có RabbitMQ.
[ ] Chưa có idempotency key.
[ ] Chưa có webhook signature.
[ ] Chưa có database thật cho DiscountService/PaymentService.
[ ] Chưa có Saga.
```

Lý do:

```text
Các phần này thuộc phase sau.
Không nhồi vào Checkpoint 4 để tránh làm vỡ lộ trình.
```

---

## 17. Chuẩn bị sang Phase 1.5

Sau Checkpoint 4, phase tiếp theo là RabbitMQ + Reliability.

Bài tiếp theo:

```text
Buổi 19: RabbitMQ Intro + First Integration Event
```

Mục tiêu sắp tới:

```text
Từ synchronous flow baseline
→ bắt đầu event-driven communication
→ publish/consume event đầu tiên
→ chuẩn bị NotificationWorker, Retry/DLQ, Idempotency, Outbox basic
```

Tư duy cần mang sang bài 19:

```text
Không phải việc gì cũng gọi REST sync.
Một số việc nên phát event để service khác xử lý async.
```

Ví dụ tương lai:

```text
OrderCreated
PaymentSucceeded
PaymentFailed
NotificationRequested
```

---

## 18. Review cuối checkpoint

Tự ghi lại 5 dòng:

```text
1. Phase 1.4 đã build được gì?
2. Service boundary hiện tại là gì?
3. Flow nào đã chạy bằng Postman?
4. Production risk lớn nhất là gì?
5. Bài 19 RabbitMQ sẽ giúp giải quyết vấn đề nào?
```

Gợi ý câu trả lời:

```text
Phase 1.4 giúp MicroShop có core flow của e-commerce: order, checkout, discount và payment/webhook baseline.
Các service đã bắt đầu có boundary riêng.
Tuy nhiên flow vẫn còn synchronous và chưa xử lý reliability production.
RabbitMQ phase tiếp theo sẽ giúp bắt đầu event-driven communication để tách service và xử lý async tốt hơn.
```

---

## 19. Không làm trong checkpoint này

Không làm:

```text
[ ] Không thêm RabbitMQ code.
[ ] Không thêm Kafka.
[ ] Không thêm Outbox.
[ ] Không thêm Saga.
[ ] Không thêm InventoryService.
[ ] Không thêm ShippingService.
[ ] Không rewrite toàn bộ Checkout.
[ ] Không đổi lộ trình.
```

Checkpoint này là để khóa phase, không phải mở thêm scope mới.

---

## 20. Output cuối cùng của checkpoint

Sau checkpoint, nên có:

```text
docs/adr/0004-ordering-discount-payment-boundary.md
docs/demo/checkpoint-4-demo-script.md
docs/interview/checkpoint-4-interview-notes.md
Postman Collection: MicroShop - Checkpoint 4
Postman Environment: MicroShop Local
```

Nếu chưa tạo đủ file, ít nhất phải có ghi chú tương đương trong Notion/Obsidian.

Câu chốt:

```text
Sau Phase 1.4, bạn không chỉ “học xong Order/Payment”.
Bạn cần có khả năng demo flow, giải thích boundary, nhìn ra risk production và biết phase sau sẽ xử lý risk đó bằng RabbitMQ/Reliability.
```
