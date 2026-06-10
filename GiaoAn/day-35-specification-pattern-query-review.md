---
day: 35
title: "Specification Pattern Lite + Query Criteria"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
encoding_note: "UTF-8 Markdown tiếng Việt chuẩn"
style: "learning-practice"
---

# Day 35: Specification Pattern Lite + Query Criteria

## 0. Hôm nay học gì?

Hôm nay mình học cách tổ chức query/filter logic để endpoint và repository sạch hơn.

Bài này dùng Specification Pattern Lite, không dựng generic framework phức tạp.

## 1. Vì sao cần bài này?

Query ban đầu thường đơn giản. Nhưng khi có search, filter, sort, paging, price range..., logic dễ bị rải ở nhiều nơi.

Query Criteria giúp gom ý định query lại rõ hơn mà không over-engineering.

## 2. Khái niệm cốt lõi

### Specification Pattern là gì?

Specification là một rule có thể tái sử dụng.

Ví dụ:

```text
Product có giá trong khoảng min/max.
Product match keyword.
Order thuộc customer X.
```

### Vì sao dùng Lite?

Vì repo hiện tại CatalogService còn khá đơn giản. Dùng `ProductQueryCriteria` là đủ, không cần generic framework.

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


Target khuyến nghị:

```text
CatalogService product query/search
```

Route:

```text
GET /products
GET /products/search
GET /products/count
GET /products/price-range?minPrice=0&maxPrice=1000
```

Lưu ý: không document `/products/price-range` trống nếu code cần `minPrice` và `maxPrice`.

## 4. Thực hành từng bước

### Bước 1: Search query/filter logic

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "search|filter|Where|OrderBy|price|count|range|Specification|Criteria"
```

### Bước 2: Inspect CatalogService

```powershell
Get-ChildItem Services/CatalogService -Recurse -Filter *Endpoints.cs
Get-ChildItem Services/CatalogService -Recurse -Filter *.cs
```

### Bước 3: Tạo docs

```text
docs/patterns/specification-pattern.md
docs/patterns/day-35-specification-review.md
docs/backlog/day-35-query-hardening-backlog.md
```

### Bước 4: Nếu implement, dùng criteria nhỏ

```csharp
public sealed record ProductQueryCriteria(
    string? SearchTerm,
    decimal? MinPrice,
    decimal? MaxPrice,
    int Page,
    int PageSize)
{
    public int Offset => (Page - 1) * PageSize;
}
```

### Bước 5: Test route

```text
GET {{catalog_url}}/products
GET {{catalog_url}}/products/search?keyword=phone
GET {{catalog_url}}/products/count
GET {{catalog_url}}/products/price-range?minPrice=0&maxPrice=1000
```

## 5. Kết quả kỳ vọng

Kỳ vọng:

```text
Query/filter logic được review.
Biết có nên dùng criteria không.
Không tạo generic framework quá sớm.
Price-range docs/test có minPrice/maxPrice.
```

## 6. Lỗi hay gặp

```text
Lỗi 1: Over-engineering bằng generic Specification framework.
Lỗi 2: Đổi API contract khi không cần.
Lỗi 3: Quên query params của price-range.
Lỗi 4: Criteria chứa SQL quá sớm.
```

## 7. Tổng kết bài học

Day 35 giúp mình học cách làm query code sạch hơn mà vẫn thực tế. Senior không phải lúc nào cũng tạo abstraction to, mà biết chọn abstraction vừa đủ.

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
Commit: Day 35: Specification Lite Query Criteria
Tag: day-35-specification-lite-query-criteria
```
