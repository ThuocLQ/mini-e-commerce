---
day: 34
title: "FluentValidation Pipeline + Mapping"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
encoding_note: "UTF-8 Markdown tiếng Việt chuẩn"
style: "learning-practice"
---

# Day 34: FluentValidation Pipeline + Mapping

## 0. Hôm nay học gì?

Hôm nay mình học cách validate input và mapping DTO rõ ràng hơn.

Bài này làm một slice nhỏ, không thêm validator cho toàn bộ repo.

## 1. Vì sao cần bài này?

Nếu request xấu đi sâu vào business/database mới fail thì debug rất mệt. Validation tốt giúp reject sớm, trả lỗi rõ ràng.

Mapping tốt giúp API không leak persistence model ra ngoài.

## 2. Khái niệm cốt lõi

### Validation

Validation trả lời:

```text
Request này có hợp lệ không?
Sai field nào?
Sai vì sao?
```

### FluentValidation

FluentValidation giúp viết rule rõ:

```csharp
RuleFor(x => x.CustomerName).NotEmpty();
RuleFor(x => x.TotalAmount).GreaterThanOrEqualTo(0);
```

### Mapping

Mapping là chuyển đổi:

```text
Request DTO -> command/query/read model
Domain/read model -> response DTO
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


Target khuyến nghị:

```text
OrderQueryService
POST /debug/order-summaries
DTO: DebugUpsertOrderSummaryRequest
```

Repo-aware note:

```text
OrderQueryService hiện đã có FluentValidation.DependencyInjectionExtensions và validator.
Day 34 không nói như thể FluentValidation còn thiếu.
Trọng tâm là review/giải thích wiring hiện có và bổ sung docs/test nếu thiếu.
```

## 4. Thực hành từng bước

### Bước 1: Inspect validation/mapping

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "FluentValidation|IValidator|Validate|ValidationResult|MapTo|Mapper|AutoMapper|Mapster|BadRequest"
```

### Bước 2: Inspect package/wiring

```powershell
Get-ChildItem Services -Recurse -Filter *.csproj |
  Select-String -Pattern "FluentValidation|AutoMapper|Mapster"

Get-ChildItem Services/OrderQueryService -Recurse -Filter *.cs |
  Select-String -Pattern "FluentValidation|DependencyInjectionExtensions|IValidator|Validator"
```

### Bước 3: Review validator

Validator mong muốn:

```csharp
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

### Bước 4: Test invalid payload

```json
{
  "customerName": "",
  "totalAmount": -1,
  "currency": "VN"
}
```

### Bước 5: Viết docs

Tạo:

```text
docs/api/validation-and-mapping-standard.md
docs/api/day-34-validation-mapping-notes.md
```

## 5. Kết quả kỳ vọng

Kỳ vọng:

```text
Biết validator hiện đang được wire thế nào.
Invalid request trả 400 rõ ràng.
Không crash service.
Không ghi data sai.
Mapping target endpoint được review.
```

## 6. Lỗi hay gặp

```text
Lỗi 1: Nghĩ OrderQueryService chưa có FluentValidation dù repo đã có.
Lỗi 2: Add package lại không cần thiết.
Lỗi 3: Tạo global validation framework quá sớm.
Lỗi 4: Validate nhưng không test response thật.
```

## 7. Tổng kết bài học

Day 34 giúp mình học cách đưa validation vào có kiểm soát: bắt đầu từ một slice nhỏ, test lỗi thật, rồi mới mở rộng sau.

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
Commit: Day 34: FluentValidation Mapping Slice
Tag: day-34-fluentvalidation-mapping-slice
```
