---
day: 45
title: "Timeout / Retry / Circuit Breaker"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
style: "production-failure-scenario"
phase: "Stage 2 - Production Hardening"
---

# Day 45: Timeout / Retry / Circuit Breaker

## 0. Vấn đề production

Distributed system không chỉ fail bằng exception rõ ràng. Nó có thể:

```text
Chậm.
Treo.
Timeout.
Downstream chập chờn.
Retry quá nhiều làm sự cố nặng hơn.
```

Vấn đề:

```text
Nếu CatalogService chậm/down, BasketService có bị treo không?
Nếu PaymentService timeout, OrderingService có retry bừa không?
Nếu downstream lỗi liên tục, có fail fast không?
```


## 1. Chọn package trước khi implement

Không chỉ search keyword. Phải quyết định rõ dùng gì.

Có 2 hướng phổ biến:

```text
Option A: Polly
    Phù hợp nếu project đang dùng Polly hoặc muốn explicit policy rõ.

Option B: Microsoft.Extensions.Http.Resilience
    Phù hợp với .NET 8+ và HttpClientFactory modern resilience pipeline.
```

Quy tắc:

```text
Nếu repo đã có Polly -> ưu tiên tiếp tục Polly.
Nếu repo chưa có gì và đang .NET 8 -> cân nhắc Microsoft.Extensions.Http.Resilience.
Không trộn cả hai nếu không có lý do rõ.
```

Inspect:

```powershell
Get-ChildItem . -Recurse -Include *.csproj,*.cs |
  Select-String -Pattern "Polly|Microsoft.Extensions.Http.Resilience|AddStandardResilienceHandler|AddPolicyHandler|CircuitBreaker|Retry|Timeout"
```

## 2. Kiến thức cần nắm

### 1.1. Timeout

Timeout giới hạn thời gian chờ downstream.

Không có timeout:

```text
Request giữ tài nguyên lâu.
Connection pool/thread pool bị cạn.
Caller có thể bị kéo chết theo downstream.
```

### 1.2. Retry

Retry chỉ hợp lý cho lỗi tạm thời và operation an toàn.

Có thể retry:

```text
GET/read transient failure.
HTTP 503/timeout ngắn.
Network glitch.
```

Không retry bừa:

```text
POST tạo order/payment nếu không có idempotency key.
400 validation error.
401/403 auth error.
Business rule error.
```

### 1.3. Circuit breaker

Circuit breaker ngắt gọi downstream tạm thời khi lỗi liên tục.

Mục tiêu:

```text
Fail fast.
Bảo vệ caller.
Cho downstream hồi phục.
```

### 1.4. Retry storm

Retry quá nhiều làm traffic tăng đột biến.

Ví dụ:

```text
100 request x retry 3 lần = 300 calls.
Downstream đang yếu càng chết nhanh hơn.
```

### 1.5. Idempotency trước retry

Operation có side effect muốn retry phải có idempotency key.

Ví dụ:

```text
Checkout command.
Create payment.
Process webhook.
```

## 3. Policy matrix bắt buộc

Dùng bảng này để quyết định, không “thấy lỗi là retry”.

```text
Call type                          Timeout   Retry                  Circuit breaker        Ghi chú
---------------------------------- --------- ---------------------- ---------------------- ------------------------------
GET Catalog product                Có        Có, ít lần             Có                     Read call, transient retry OK
Basket -> Catalog validate         Có        Có chọn lọc            Có                     Không retry lỗi business
POST Checkout                      Có        Không retry bừa        Cẩn thận               Cần idempotency key nếu retry
POST Create Payment                Có        Future/saga target     Future                 Chỉ ghi current nếu repo có call thật
Webhook receive                    Có        Không tự retry trong request Không phù hợp trực tiếp Provider sẽ retry
Gateway -> downstream              Có        Có chọn lọc            Có                     Fail fast khi downstream chết
Outbox publisher -> broker         Có        Có, backoff            Có thể                 At-least-once, duplicate expected
ProjectionWorker -> MongoDB        Có        Có cẩn thận            Không phải trọng tâm    Cần idempotent projection
```

Rule ngắn:

```text
Read call: retry được nếu transient.
Write/side-effect call: chỉ retry nếu có idempotency.
Business error: không retry.
Downstream chết liên tục: circuit breaker để fail fast.
```

## 4. Repo cần nhìn vào đâu

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "HttpClient|Refit|AddRefitClient|Grpc|AddGrpcClient|Timeout|Retry|CircuitBreaker|Polly|Resilience|WaitAndRetry|AddStandardResilienceHandler|AddPolicyHandler"
```

Cần xác nhận:

```text
Service nào gọi service nào?
Có timeout explicit không?
Có retry policy không?
Có circuit breaker không?
Operation retry có idempotency key không?
```

## 5. Thực hành chính

### Bước 1: Lập danh sách outbound calls thật

Điền theo repo:

```text
Caller | Downstream | Operation | Read/Write | Current timeout | Current retry | Risk
-------|------------|-----------|------------|-----------------|---------------|-----
BasketService | CatalogService | validate product | Read | ? | ? | ?
OrderingService | DiscountService | apply discount | Read/Business | ? | ? | ?
OrderingService | PaymentService | create payment | Future/current? | ? | ? | ?
Gateway | Services | proxy | Mixed | ? | ? | ?
```

### Bước 2: Chọn một slice để harden

Khuyến nghị:

```text
BasketService -> CatalogService
```

Vì đây thường là read/validate call, dễ demo timeout/retry/circuit breaker hơn payment write.

### Bước 3: Áp dụng policy tối thiểu

Gợi ý local demo:

```text
Timeout: 2-3 giây
Retry: 1-2 lần cho transient error
Circuit breaker: open sau N lỗi liên tiếp
```

Không retry:

```text
400/validation/business error
```

### Bước 4: Test downstream down

```powershell
docker compose stop catalogservice
```

Gọi endpoint Basket phụ thuộc Catalog.

Quan sát:

```text
Response có trả nhanh không?
Có error format/traceId không?
Service caller có crash không?
Log có downstream unavailable/timeout không?
```

Start lại:

```powershell
docker compose start catalogservice
```

### Bước 5: Test gọi liên tục khi downstream down

Quan sát:

```text
Retry attempt có giới hạn không?
Circuit breaker có open không?
Sau khi open có fail fast không?
Khi service hồi phục có recover không?
```

## 6. Failure drill

Drill chính:

```text
Stop CatalogService rồi gọi BasketService endpoint cần Catalog.
```

Kỳ vọng:

```text
Không treo lâu.
Không crash caller.
Response lỗi có kiểm soát.
Log đủ thông tin.
```

Drill phụ:

```text
Gọi liên tục khi downstream down.
```

Kỳ vọng:

```text
Circuit breaker open.
Fail fast thay vì spam downstream.
```

## 7. Câu hỏi interview

```text
1. Timeout giải quyết vấn đề gì?
2. Retry khi nào nên dùng?
3. Khi nào tuyệt đối không retry?
4. Circuit breaker khác retry ở đâu?
5. Retry storm là gì?
6. Vì sao create payment cần idempotency key nếu retry?
7. Policy cho GET Catalog khác POST Payment thế nào?
8. Nếu downstream chết, caller nên fail fast hay chờ lâu?
```

## 8. Kết luận

```text
Resilience không phải cứ thêm retry.
Senior answer đúng phải có policy:
- call nào timeout
- call nào retry
- call nào circuit break
- call nào cần idempotency trước retry
- lỗi nào không retry
```

## 9. Optional commit

```text
Commit: Day 45: Timeout Retry Circuit Breaker
Tag: day-45-timeout-retry-circuit-breaker
```
