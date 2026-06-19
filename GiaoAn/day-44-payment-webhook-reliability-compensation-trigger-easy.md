---
day: 44
title: "Payment Webhook Reliability + Compensation Trigger"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
language: "vi"
style: "easy-production-learning"
---

# Day 44: Payment Webhook Reliability + Compensation Trigger

## 0. Nói dễ hiểu: bài này học gì?

Payment webhook là lúc hệ thống bên ngoài gọi HTTP vào hệ thống mình để báo kết quả payment.

Ví dụ:

```text
Payment provider
-> POST /webhooks/payment
-> MicroShop nhận kết quả success/fail
```

Webhook không đáng tin tuyệt đối:

```text
Có thể gửi trùng.
Có thể gửi muộn.
Có thể bị fake nếu không verify.
Có thể gửi success sau khi order đã cancelled.
```

Day 39 đã có:

```text
WebhookLogs cơ bản + ProviderEventId.
```

Day 44 không làm lại WebhookLog từ đầu.

Day 44 harden thêm:

```text
Signature verification.
Payload audit.
Publish PaymentSucceeded/PaymentFailed.
Compensation trigger.
```

---

## 1. Tình huống lỗi thực tế

### Case 1: Duplicate webhook

Provider gửi cùng `ProviderEventId` 2 lần.

Nếu xử lý 2 lần:

```text
Có thể publish PaymentSucceeded 2 lần.
Order/Saga nhận duplicate event.
Side effect bị lặp.
```

### Case 2: Fake webhook

Ai đó gọi endpoint webhook giả mạo.

Nếu không verify:

```text
Attacker có thể báo PaymentSucceeded giả.
```

### Case 3: Late webhook

Order đã Cancelled nhưng webhook PaymentSucceeded đến sau.

Nếu xử lý bừa:

```text
Order bị mark Paid sai.
```

---

## 2. Webhook production minimum

Một webhook production-minded cần:

```text
1. ProviderEventId để chống duplicate.
2. WebhookLog để lưu lịch sử.
3. Signature/shared secret để chống fake webhook.
4. PayloadHash hoặc raw payload để audit/debug.
5. Publish internal event đúng một lần.
6. Return 2xx nhanh nếu accept/duplicate safe.
7. Compensation trigger khi state conflict.
```

---

## 3. Repo hiện tại cần kiểm tra

```powershell
Get-ChildItem Services/PaymentService -Recurse -Filter *.cs |
  Select-String -Pattern "Webhook|webhooks|WebhookLog|ProviderEventId|PaymentSucceeded|PaymentFailed|Signature|PayloadHash|RawPayload|Outbox|Publish"
```

Cần điền bảng:

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

Nhớ: nếu Day 39 đã implement `WebhookLogs + ProviderEventId`, không được ghi là chưa có.

---

## 4. Signature verification là gì?

Mục tiêu: xác minh webhook đúng là từ provider.

Cách đơn giản ở training-stage:

```text
Provider gửi header signature.
MicroShop dùng shared secret để tính lại signature từ raw body.
Nếu match -> accept.
Nếu không match -> reject.
```

Pseudo:

```text
rawBody = read request body
expectedSignature = HMAC_SHA256(rawBody, sharedSecret)
actualSignature = request header

if actualSignature != expectedSignature:
    save WebhookLog(signature failed)
    return 401/400
```

Nếu chưa muốn implement HMAC thật trong Day 44, tối thiểu phải design:

```text
signature header name
shared secret config key
payloadHash field
signatureStatus field
behavior khi invalid signature
```

---

## 5. Publish internal event đúng một lần

Target flow:

```text
Receive webhook
-> verify signature
-> check ProviderEventId duplicate
-> save/update WebhookLog
-> publish PaymentSucceeded hoặc PaymentFailed
-> return 2xx
```

Rule:

```text
Nếu ProviderEventId đã processed:
    return 200
    không publish event lần 2
```

Nếu repo chưa có event contract:

```text
PaymentSucceeded/PaymentFailed là contract cần tạo nếu implement.
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

---

## 6. Compensation trigger

Case quan trọng:

```text
PaymentSucceeded đến sau khi OrderCancelled.
```

Không xử lý bừa:

```text
Không tự mark order Paid nếu order đã Cancelled.
```

Hướng xử lý:

```text
Mark RefundRequired.
Publish RefundRequested.
Log CompensationRequired.
Manual intervention nếu chưa có refund flow.
```

Rule gợi ý:

```text
PaymentSucceeded + OrderPendingPayment
-> mark Paid

PaymentSucceeded + OrderCancelled
-> CompensationRequired / RefundRequired

PaymentFailed + OrderPendingPayment
-> cancel order

PaymentFailed + OrderPaid
-> log conflict, không auto cancel
```

---

## 7. Failure drill

### Drill 1: Duplicate webhook

Gửi cùng `ProviderEventId` 2 lần.

Kỳ vọng:

```text
WebhookLog nhận biết duplicate.
PaymentSucceeded/PaymentFailed chỉ publish một lần.
Response duplicate vẫn an toàn.
```

### Drill 2: Invalid signature

Gửi webhook sai signature.

Kỳ vọng:

```text
Không publish event.
Không update order/payment như success.
WebhookLog ghi signature failed.
Return 401/400 theo policy.
```

### Drill 3: Late success

Giả lập:

```text
Order đã Cancelled.
Webhook PaymentSucceeded đến sau.
```

Kỳ vọng:

```text
Không mark Paid bừa.
Tạo CompensationRequired/RefundRequired signal.
```

---

## 8. Câu hỏi interview

```text
1. Webhook khác RabbitMQ/Kafka ở đâu?
2. Vì sao webhook hay duplicate?
3. ProviderEventId dùng để làm gì?
4. Vì sao cần WebhookLog?
5. Signature verification chống gì?
6. Vì sao cần payloadHash/raw payload?
7. Vì sao duplicate webhook vẫn nên return 2xx?
8. Làm sao đảm bảo PaymentSucceeded chỉ publish một lần?
9. PaymentSucceeded sau OrderCancelled xử lý sao?
```

---

## 9. Kết luận

```text
Webhook production không phải chỉ là có endpoint HTTP.

Sau Day 44, tư duy đúng là:
- webhook phải verify được
- duplicate phải an toàn
- payload phải audit/debug được
- internal event chỉ publish một lần
- state conflict phải có compensation trigger
```
