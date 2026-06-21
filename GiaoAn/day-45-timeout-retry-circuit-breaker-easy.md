---
day: 45
title: "Timeout / Retry / Circuit Breaker"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
language: "vi"
style: "easy-production-learning"
---

# Day 45: Timeout / Retry / Circuit Breaker

## 0. Nói dễ hiểu: bài này học gì?

Trong microservices, lỗi không phải lúc nào cũng là exception rõ ràng.

Downstream có thể:

```text
Chậm.
Treo.
Lúc được lúc lỗi.
Timeout.
```

Nếu service A gọi service B mà không có timeout/resilience:

```text
B chết chậm.
A chờ mãi.
A cũng bị kéo chết.
```

Bài này học cách dùng:

```text
Timeout
Retry
Circuit Breaker
```

nhưng quan trọng hơn: **biết khi nào không được retry**.

---

## 1. Tình huống lỗi thực tế

Ví dụ:

```text
BasketService gọi CatalogService để validate product.
CatalogService bị down hoặc chậm.
```

Nếu không có timeout:

```text
BasketService request bị treo lâu.
User chờ.
Thread/connection bị giữ.
```

Nếu retry bừa:

```text
CatalogService đang chết.
BasketService retry liên tục.
Traffic tăng.
CatalogService càng chết.
```

Nếu có circuit breaker:

```text
Sau một số lỗi, BasketService fail fast.
Không spam CatalogService nữa.
```

---

## 2. Chọn package trước khi implement

Không chỉ nói chung chung “thêm retry”.

Cần chọn package.

### Option A: Polly

Dùng nếu:

```text
Repo đã dùng Polly.
Muốn policy explicit, dễ nhìn.
```

### Option B: Microsoft.Extensions.Http.Resilience

Dùng nếu:

```text
Project .NET 8+.
Đang dùng HttpClientFactory.
Muốn dùng resilience pipeline mới hơn.
```

Rule:

```text
Nếu repo đã có Polly -> tiếp tục Polly.
Nếu repo chưa có gì và đang .NET 8 -> cân nhắc Microsoft.Extensions.Http.Resilience.
Không trộn cả hai nếu không có lý do rõ.
```

Inspect:

```powershell
Get-ChildItem . -Recurse -Include *.csproj,*.cs |
  Select-String -Pattern "Polly|Microsoft.Extensions.Http.Resilience|AddStandardResilienceHandler|AddPolicyHandler|CircuitBreaker|Retry|Timeout"
```

---

## 3. Kiến thức cần nắm

### 3.1. Timeout

Timeout giới hạn thời gian chờ downstream.

Ví dụ:

```text
Nếu CatalogService không trả lời sau 3 giây
-> fail thay vì chờ mãi
```

Timeout bảo vệ caller.

### 3.2. Retry

Retry là gọi lại khi lỗi có thể tạm thời.

Nên retry:

```text
GET/read call bị timeout ngắn.
HTTP 503 transient.
Network glitch.
```

Không retry bừa:

```text
POST tạo order.
POST tạo payment.
400 validation error.
401/403 auth error.
Business rule error.
```

### 3.3. Circuit breaker

Circuit breaker giống cầu dao.

Nếu downstream lỗi liên tục:

```text
Circuit open.
Các request sau fail fast.
Sau một thời gian thử lại.
Nếu downstream hồi phục thì close.
```

### 3.4. Retry storm

Retry storm là khi retry làm traffic tăng và khiến hệ thống chết nặng hơn.

Ví dụ:

```text
100 request
mỗi request retry 3 lần
=> 300 calls vào service đang chết
```

### 3.5. Idempotency trước retry

Nếu operation có side effect, retry có thể tạo side effect nhiều lần.

Ví dụ:

```text
Retry create payment
-> có thể double charge nếu không có idempotency key
```

---

## 4. Policy matrix dễ nhớ

```text
Call type                  Timeout | Retry | Circuit breaker | Note
---------------------------|---------|-------|-----------------|-----
GET Catalog product         Có      | Có ít | Có              | Read call, retry transient OK
Basket -> Catalog validate  Có      | Có ít | Có              | Không retry business error
POST Checkout               Có      | Không bừa | Cẩn thận     | Cần idempotency key nếu retry
POST Create Payment         Có      | Chỉ khi idempotent | Có | Nếu chưa có flow thật thì là future target
Webhook receive             Có      | Không tự retry trong request | Không trực tiếp | Provider sẽ retry
Gateway -> downstream       Có      | Có chọn lọc | Có        | Fail fast khi downstream chết
Outbox publisher -> broker  Có      | Có backoff | Có thể     | Duplicate expected
ProjectionWorker -> MongoDB Có      | Có cẩn thận | Không trọng tâm | Projection phải idempotent
```

Rule ngắn:

```text
Read call có thể retry.
Write call chỉ retry nếu có idempotency.
Business error không retry.
Downstream chết liên tục thì circuit breaker.
```

---

## 5. Refit/gRPC lưu ý

Nếu dùng Refit qua `HttpClientFactory`:

```text
Có thể gắn resilience handler vào HttpClient.
```

Nếu dùng gRPC client:

```text
Không bê nguyên HTTP policy sang nếu chưa verify.
gRPC có deadline/retry config riêng.
```

Kết luận:

```text
Basket -> Catalog REST/Refit là slice dễ demo nhất.
gRPC resilience để sau nếu chưa chắc.
```

---

## 6. Repo cần kiểm tra

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "HttpClient|Refit|AddRefitClient|Grpc|AddGrpcClient|Timeout|Retry|CircuitBreaker|Polly|Resilience|WaitAndRetry|AddStandardResilienceHandler|AddPolicyHandler"
```

Điền bảng:

```text
Caller | Downstream | Operation | Read/Write | Current timeout | Current retry | Package | Risk
-------|------------|-----------|------------|-----------------|---------------|---------|-----
BasketService | CatalogService | validate product | Read | ? | ? | ? | ?
OrderingService | DiscountService | apply discount | Read/business | ? | ? | ? | ?
OrderingService | PaymentService | create payment | Future/current? | ? | ? | ? | ?
Gateway | Services | proxy | Mixed | ? | ? | ? | ?
```

Lưu ý:

```text
OrderingService -> PaymentService create payment chỉ ghi là current nếu repo thật có.
Nếu chưa có thì ghi future/saga target.
```

---

## 7. Lab chính: BasketService gọi CatalogService

Chọn slice này vì:

```text
Đây là read/validate call.
Dễ test.
Ít nguy cơ double side-effect.
```

### Bước 1: Chạy hệ thống

```powershell
docker compose up -d --build catalogservice basketservice
```

Nếu Basket cần Redis hoặc service khác thì bật thêm theo repo thật.

### Bước 2: Gọi endpoint Basket cần Catalog

Dùng endpoint thật trong repo.

Ví dụ dạng:

```text
GET /basket/products/{productId}/validate
POST /basket/preview-item
```

Không đoán endpoint nếu repo khác.

### Bước 3: Stop CatalogService

```powershell
docker compose stop catalogservice
```

Gọi lại endpoint Basket.

Check logs:

```powershell
docker compose logs basketservice --tail 100
docker compose logs catalogservice --tail 100
```

Quan sát:

```text
Request treo bao lâu?
Có timeout không?
Có retry không?
Response có kiểm soát không?
BasketService có crash không?
```

Nếu timeout đặt 2-3 giây mà request treo 30 giây:

```text
Policy chưa ăn hoặc đang đặt sai chỗ.
```

### Bước 4: Gọi liên tục để xem circuit breaker

Gọi endpoint nhiều lần.

Kỳ vọng nếu có circuit breaker:

```text
Sau một số lỗi, circuit open.
Request fail fast.
Không spam CatalogService.
```

### Bước 5: Start lại CatalogService

```powershell
docker compose start catalogservice
```

Gọi lại sau một lúc.

Kỳ vọng:

```text
Circuit half-open/closed.
Request hoạt động lại.
```

---

## 8. Failure drill

### Drill chính

```text
Stop CatalogService.
Gọi BasketService endpoint phụ thuộc Catalog.
```

Kỳ vọng:

```text
Không treo lâu.
Không crash BasketService.
Có log downstream unavailable/timeout.
Response lỗi có kiểm soát.
```

### Drill phụ

```text
Gọi liên tục khi CatalogService down.
```

Kỳ vọng:

```text
Circuit breaker open.
Fail fast.
Không retry storm.
```

---

## 9. Câu hỏi interview

```text
1. Timeout giải quyết vấn đề gì?
2. Retry khi nào nên dùng?
3. Khi nào tuyệt đối không retry?
4. Circuit breaker khác retry ở đâu?
5. Retry storm là gì?
6. Vì sao create payment cần idempotency key nếu retry?
7. Polly và Microsoft.Extensions.Http.Resilience khác nhau thế nào ở mức sử dụng?
8. Refit gắn resilience thế nào?
9. gRPC có dùng y nguyên HTTP policy không?
10. Nếu downstream chết, caller nên fail fast hay chờ lâu?
```

---

## 10. Kết luận

```text
Resilience không phải cứ thêm retry.

Senior answer đúng phải nói được:
- call nào timeout
- call nào retry
- call nào không retry
- call nào cần circuit breaker
- call nào cần idempotency trước retry
```
