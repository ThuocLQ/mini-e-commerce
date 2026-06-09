# Day 35: Specification Pattern Lite / Query Criteria

## 0. Vị trí hiện tại

Bạn đã hoàn thành Day 34 validation/mapping slice.

Day 35 focuses on query logic organization.

Mục tiêu:

```text
Avoid scattering query/filter rules across endpoints and repositories.
Introduce a lightweight query criteria / specification-lite approach in one target slice.
Do not force a full generic Specification Pattern framework.
```

## 1. Bối cảnh repo hiện tại

Sự thật hiện tại của repo:

```text
Services:
- Services/ApiGateway
- Services/CatalogService
- Services/BasketService
- Services/OrderingService
- Services/DiscountService
- Services/IdentityService
- Services/PaymentService
- Services/OrderQueryService

Background workers:
- Services/NotificationWorker
- Workers/ProjectionWorker

Shared:
- BuildingBlocks.Contracts
- MicroShop.AppHost
- MicroShop.ServiceDefaults
```

Các route quan trọng:

```text
GET /order-summaries
GET /order-summaries/{orderId}
POST /debug/order-summaries

GET /orders
GET /orders/{id}
POST /orders/checkout
GET /debug/outbox

GET /products
GET /products/{id}
GET /products/search
GET /products/count
GET /products/price-range
POST /products
PUT /products/{id}
DELETE /products/{id}

GET /basket/{userId}
POST /basket/{userId}/items
POST /basket/{userId}/items-grpc
PUT /basket/{userId}/items/{productId}
DELETE /basket/{userId}/items/{productId}
PUT /basket/{userId}/clear
DELETE /basket/{userId}
GET /basket/products/{productId}/validate
GET /basket/products/{productId}/validate-grpc
POST /basket/preview-item
POST /basket/preview-item-grpc
GET /basket/products/{productId}/compare-communication

GET /discounts/{code}
POST /discounts/apply

POST /auth/login
GET /auth/me

POST /payments
GET /payments/{id}
POST /webhooks/payment
POST /payments/webhooks/payment

GET /health
GET /alive
```

Không dùng:

```text
/orders/read-model
ORD-900
CUST-900
```


## 2. Mục tiêu

```text
[ ] Specification Pattern is understood.
[ ] Current query/filter logic is reviewed.
[ ] One target slice is selected.
[ ] Specification/query criteria docs are created.
[ ] Optional minimal implementation is added if safe.
[ ] Postman verifies query behavior did not regress.
```

Output chính:

```text
docs/patterns/specification-pattern.md
docs/patterns/day-35-specification-review.md
docs/backlog/day-35-query-hardening-backlog.md
```

Output code tùy chọn:

```text
Query criteria object in one service
ProductQueryCriteria or ProductSearchCriteria
Repository method accepting query criteria
```

## 3. Giới hạn phạm vi

Nên làm:

```text
[ ] Review query/filter logic.
[ ] Introduce Specification Pattern concept.
[ ] Apply to one small slice if useful.
[ ] Keep behavior unchanged.
[ ] Add tests/Postman checks around search/filter.
```

Không làm:

```text
[ ] Do not rewrite all repositories.
[ ] Do not build a generic framework too early.
[ ] Do not change API contracts.
[ ] Do not change database schema.
[ ] Do not change RabbitMQ/Kafka behavior.
[ ] Do not claim all queries use specifications.
```

Điều phần này chứng minh:

```text
MicroShop has a direction for reusable query rules.
One query slice can be made cleaner without broad refactor.
```

Điều phần này chưa chứng minh:

```text
All repositories are standardized.
All query performance issues are solved.
```

## 4. Kiểm tra trước khi làm

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "search|filter|Where|OrderBy|price|count|range|Specification|Criteria"
Get-ChildItem Services/CatalogService -Recurse -Filter *Endpoints.cs
Get-ChildItem Services/CatalogService -Recurse -Filter *.cs
Get-ChildItem Services/OrderingService -Recurse -Filter *Endpoints.cs
Get-ChildItem Services/OrderingService -Recurse -Filter *.cs
```

## 5. What is Specification Pattern Lite?

A specification represents a reusable business/query rule.

Examples:

```text
Products with price between min and max.
Products matching search keyword.
Active orders for customer.
Orders created within date range.
```

Benefits:

```text
Reusable query rules.
More testable logic.
Less duplicated filtering.
Clearer intent.
```

Risk:

```text
Over-engineering if used for simple queries.
Too generic abstraction can become harder than direct code.
CatalogService currently has simple Dapper query methods such as SearchAsync, GetByPriceRangeAsync, and GetAllAsync.
Day 35 should not force a full Specification Pattern framework if a simple query criteria object is enough.
```

## 6. Target slice recommendation

Recommended target:

```text
CatalogService product query/search
```

Why:

```text
Catalog has query routes:
GET /products
GET /products/search
GET /products/count
GET /products/price-range
```

Alternative:

```text
OrderingService GET /orders query filters if they exist.
```

Hôm nay không target ProjectionWorker.

## 7. Documentation first

Tạo:

```text
docs/patterns/specification-pattern.md
```

Bao gồm:

```text
Goal
When to use
When not to use
MicroShop candidate
Example query criteria objects
Current stage
```

## 8. Minimal implementation option

Chỉ implement nếu query code của CatalogService đã sẵn sàng.

Tránh abstraction qua generic gia tao.

Với Dapper, ưu tiên object filter/sort/paging nhỏ gọn.

Cách local tốt hơn:

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

Repository method:

```csharp
Task<IReadOnlyList<ProductDto>> SearchAsync(
    ProductQueryCriteria criteria,
    CancellationToken cancellationToken);
```

Quy tắc:

```text
Criteria expresses query intent.
Repository owns SQL translation.
Validation of Page/PageSize/MinPrice/MaxPrice follows Day 34 direction.
```

## 9. Postman query tests

Full system nếu cần:

```powershell
docker compose up -d --build
```

Gateway routes:

```text
GET {{gateway_url}}/catalog/products
GET {{gateway_url}}/catalog/products/search?keyword=phone
GET {{gateway_url}}/catalog/products/count
GET {{gateway_url}}/catalog/products/price-range?minPrice=0&maxPrice=1000
```

Gọi trực tiếp service nếu gateway không chạy:

```text
GET {{catalog_url}}/products
GET {{catalog_url}}/products/search?keyword=phone
GET {{catalog_url}}/products/count
GET {{catalog_url}}/products/price-range?minPrice=0&maxPrice=1000
```

Dùng đúng tên route/query parameter từ code.

Lưu ý quan trọng:

```text
The price-range endpoint requires minPrice and maxPrice query params.
Do not document /products/price-range without minPrice/maxPrice unless current code changes.
```

Không tự bịa parameter ngoai phan code ho tro.

## 10. Day 35 review doc

Tạo:

```text
docs/patterns/day-35-specification-review.md
```

Bao gồm:

```text
Target service
Reviewed routes
Current query logic
Candidate query criteria decision
Future work
```

## 11. Query hardening backlog

Tạo:

```text
docs/backlog/day-35-query-hardening-backlog.md
```

Bao gồm:

```text
[ ] Keep search/filter logic out of endpoint bodies.
[ ] Add validation for paging/filter inputs.
[ ] Document query parameter behavior.
[ ] Add query tests for CatalogService.
[ ] Review indexes for product search/filter paths.
[ ] Avoid complex generic specification framework too early.
```

## 12. Kế hoạch build/test

```powershell
dotnet build Services/CatalogService/CatalogService.csproj
```

If gateway used:

```powershell
dotnet build Services/ApiGateway/ApiGateway.csproj
```

Run product query/search/filter requests before and after.

## 13. Review độ phù hợp production-minded

Improves:

```text
Query intent becomes clearer.
Filter logic becomes easier to test.
Endpoints stay thinner.
```

Việc làm sau:

```text
Repository performance tuning.
Indexes.
Query tests.
Consistent paging/filtering standard.
```

## 14. Checklist đạt yêu cầu

```text
[ ] Query/filter logic is inspected.
[ ] Target service is selected.
[ ] specification-pattern.md exists.
[ ] day-35-specification-review.md exists.
[ ] day-35 query backlog exists.
[ ] Optional code changes are limited to one slice.
[ ] Query behavior is verified by Postman or manual requests.
[ ] Build passes for touched projects.
[ ] Price-range route includes minPrice and maxPrice in docs/tests.
[ ] Không đưa generic framework vào quá sớm.
```

## 15. Commit/tag tùy chọn sau review

```text
Commit: Day 35: Specification Lite Query Criteria
Tag: day-35-specification-lite-query-criteria
```

## 16. Ngày tiếp theo

```text
Day 36: Strategy Pattern + Audit Log + Advanced Identity Review
```

