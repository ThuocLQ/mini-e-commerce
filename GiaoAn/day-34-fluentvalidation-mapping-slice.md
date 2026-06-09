# Day 34: FluentValidation Pipeline + Mapping

## 0. Vị trí hiện tại

Bạn đã hoàn thành Day 33 database/schema evolution review.

Day 34 focuses on validation and DTO mapping.

Mục tiêu:

```text
Make request validation explicit, testable, and consistent in one small slice.
```

Do not apply validation everywhere today.

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
[ ] Validation strategy is documented.
[ ] Mapping strategy is documented.
[ ] One target slice uses explicit validation.
[ ] Invalid requests return documented error shape.
[ ] Postman negative tests are added.
```

Output chính:

```text
docs/api/validation-and-mapping-standard.md
docs/api/day-34-validation-mapping-notes.md
postman/MicroShop.Day34.Validation.postman_collection.json
```

Optional code output:

```text
FluentValidation validators for one target slice
Mapping helpers or explicit mapping methods for one target slice
```

## 3. Giới hạn phạm vi

Nên làm:

```text
[ ] Inspect existing validation.
[ ] Choose one target service/endpoint.
[ ] Add or document FluentValidation.
[ ] Keep mapping explicit.
[ ] Add Postman negative tests.
```

Không làm:

```text
[ ] Do not add validators to every service.
[ ] Do not introduce a huge shared validation framework today.
[ ] Do not change route shapes.
[ ] Do not change RabbitMQ/Kafka behavior.
[ ] Do not claim all validation is standardized.
```

Điều phần này chứng minh:

```text
MicroShop has a validation/mapping direction and one verified slice.
```

Điều phần này chưa chứng minh:

```text
Every request in every service is validated.
All mapping is standardized.
```

## 4. Kiểm tra trước khi làm

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "FluentValidation|IValidator|Validate|ValidationResult|MapTo|Mapper|AutoMapper|Mapster|BadRequest"
Get-ChildItem Services -Recurse -Filter *Endpoints.cs
Get-ChildItem Services -Recurse -Filter *.csproj |
  Select-String -Pattern "FluentValidation|AutoMapper|Mapster"
```

## 5. Choose target slice

Recommended target:

```text
OrderQueryService:
    POST /debug/order-summaries
    DTO: DebugUpsertOrderSummaryRequest
```

Why:

```text
It is current from recent lessons.
It is a debug endpoint, lower production risk.
It likely accepts a request body that can be validated.
POST /debug/order-summaries is expected only in Development/local learning mode.
If it is not mapped outside Development, document that behavior instead of changing it.
```

Alternative:

```text
CatalogService:
    POST /products
```

Avoid Identity/Payment as first validation slice because they are security/payment sensitive.

## 6. Validation standard

Tạo:

```text
docs/api/validation-and-mapping-standard.md
```

Bao gồm:

```text
Validate at API/Application boundary.
Keep validation messages clear.
Do not rely only on database constraints.
Return documented error format.
Do not expose persistence models directly.
Map request DTO -> command/query.
Map domain/read model -> response DTO.
Keep mapping explicit unless a mapping library is intentionally adopted.
```

## 7. Add FluentValidation package

OrderQueryService currently does not reference FluentValidation.

If target is OrderQueryService, add:

```powershell
dotnet add Services/OrderQueryService/OrderQueryService.csproj package FluentValidation
```

If the repo changes later and FluentValidation is already referenced, do not add it again.

## 8. Example validator

Adjust property names to real request DTO.

```csharp
using FluentValidation;

public sealed class DebugUpsertOrderSummaryRequestValidator
    : AbstractValidator<DebugUpsertOrderSummaryRequest>
{
    public DebugUpsertOrderSummaryRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TotalAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.ItemCount).GreaterThanOrEqualTo(0);
    }
}
```

## 9. Endpoint usage

Example:

```csharp
var validationResult = await validator.ValidateAsync(request, cancellationToken);

if (!validationResult.IsValid)
{
    return Results.ValidationProblem(
        validationResult.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray()));
}
```

If Day 32 established a local ProblemDetails helper, align with it.

Do not create a global validation pipeline today unless repo already has one.

## 10. Mapping review

For target endpoint, check:

```text
Request DTO -> application command/query/read model
Read model/domain -> response DTO
```

Avoid returning persistence model directly unless intentionally accepted for training-stage and documented.

Prefer explicit mapping for now.

## 11. Postman negative tests

Chế độ lite:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

Test invalid debug payload:

```text
POST {{order_query_url}}/debug/order-summaries
```

Payload:

```json
{
  "customerName": "",
  "totalAmount": -1,
  "currency": "VN"
}
```

Kỳ vọng:

```text
400 validation error
errors contains relevant fields
```

## 12. Day 34 notes

Tạo:

```text
docs/api/day-34-validation-mapping-notes.md
```

Bao gồm:

```text
Target service
Endpoint
Request DTO
Validator
Mapping notes
Verified negative tests
Future work
```

Cập nhật docs/README.md.

## 13. Kế hoạch build/test

```powershell
dotnet build Services/OrderQueryService/OrderQueryService.csproj
```

If Catalog target:

```powershell
dotnet build Services/CatalogService/CatalogService.csproj
```

Chạy negative test bằng Postman.

## 14. Review độ phù hợp production-minded

Improves:

```text
Bad input is rejected earlier.
Validation rules are explicit and testable.
Mapping direction is clearer.
```

Việc làm sau:

```text
Global validation pipeline.
Consistent validation errors across all services.
Mapping strategy across all services.
```

## 15. Checklist đạt yêu cầu

```text
[ ] Existing validation/mapping is inspected.
[ ] Target slice is chosen.
[ ] validation-and-mapping-standard.md exists.
[ ] DebugUpsertOrderSummaryRequestValidator exists or validation decision is documented.
[ ] Negative Postman tests exist.
[ ] Mapping is reviewed for target endpoint.
[ ] day-34-validation-mapping-notes.md exists.
[ ] Build passes for touched service.
[ ] Không đưa vào refactor validation diện rộng.
```

## 16. Commit/tag tùy chọn sau review

```text
Commit: Day 34: FluentValidation Mapping Slice
Tag: day-34-fluentvalidation-mapping-slice
```

## 17. Ngày tiếp theo

```text
Day 35: Specification Pattern
```

