---
day: 32
title: "API Versioning + Backward Compatibility + Standard Error Format"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
encoding_note: "UTF-8 Markdown tiếng Việt chuẩn"
style: "learning-practice"
---

# Day 32: API Versioning + Backward Compatibility + Standard Error Format

## 0. Hôm nay học gì?

Hôm nay mình học cách giữ API ổn định khi hệ thống lớn dần: versioning, backward compatibility và error response chuẩn.

Bài này làm một slice nhỏ, không chuẩn hóa toàn bộ API ngay.

## 1. Vì sao cần bài này?

Client cần response lỗi đoán được. Nếu mỗi service trả lỗi một kiểu, frontend/client rất khó xử lý.

Versioning và backward compatibility giúp API thay đổi mà không phá client cũ.

## 2. Khái niệm cốt lõi

### API versioning

Versioning là cách đánh dấu phiên bản API:

```text
/api/v1/products
/api/v2/products
```

Hiện tại MicroShop vẫn dùng route unversioned. Day 32 chỉ document policy.

### Backward compatibility

Non-breaking:

```text
Thêm field optional.
Thêm endpoint mới.
```

Breaking:

```text
Xóa field.
Đổi tên field.
Đổi type.
Đổi route shape.
Đổi status code.
```

### ProblemDetails

ProblemDetails là format lỗi chuẩn kiểu:

```json
{
  "type": "...",
  "title": "...",
  "status": 404,
  "detail": "...",
  "traceId": "..."
}
```

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


Target đầu tiên:

```text
OrderQueryService
GET /order-summaries/{orderId}
POST /debug/order-summaries
```

Lưu ý:

```text
Results.Problem(...) có thể không tự có traceId đúng shape.
Phải verify response thật bằng Postman.
```

## 4. Thực hành từng bước

### Bước 1: Inspect error behavior

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "ProblemDetails|Results.Problem|BadRequest|NotFound|Validation|UseExceptionHandler|AddProblemDetails"
```

### Bước 2: Tạo API versioning policy

Tạo:

```text
docs/api/api-versioning-policy.md
```

Ghi rõ:

```text
Day 32 chưa migrate toàn bộ route sang /api/v1.
Chưa cần add API versioning package.
```

### Bước 3: Tạo error handling standard

Tạo:

```text
docs/api/api-error-handling-standard.md
```

Ví dụ:

```json
{
  "type": "https://microshop.local/problems/not-found",
  "title": "Resource not found",
  "status": 404,
  "detail": "Order summary was not found.",
  "instance": "/order-summaries/11111111-1111-1111-1111-111111111111",
  "traceId": "..."
}
```

### Bước 4: Implement/check một slice nhỏ

Case:

```text
GET /order-summaries/{missingOrderId} -> 404 ProblemDetails-style
GET /order-summaries/not-a-guid -> có thể 404 do route constraint
```

### Bước 5: Test bằng Postman

```text
GET {{order_query_url}}/order-summaries/99999999-9999-9999-9999-999999999999
GET {{order_query_url}}/order-summaries/not-a-guid
```

## 5. Kết quả kỳ vọng

Kỳ vọng:

```text
Có policy versioning.
Có chuẩn error response.
Có actual response body được ghi lại.
Biết traceId có/không có trong response thật.
Không thêm global middleware quá sớm.
```

## 6. Lỗi hay gặp

```text
Lỗi 1: Thêm API versioning package khi chưa cần.
Lỗi 2: Claim toàn bộ API đã chuẩn error.
Lỗi 3: Không verify Results.Problem response thật.
Lỗi 4: Ép invalid GUID thành 400 bằng cách đổi route shape không cần thiết.
```

## 7. Tổng kết bài học

Day 32 giúp API của MicroShop có hướng ổn định hơn. Quan trọng nhất là biết chuẩn hóa từng slice nhỏ, verify response thật, không vội làm global framework.

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
Commit: Day 32: API Versioning Error Format
Tag: day-32-api-versioning-error-format
```
