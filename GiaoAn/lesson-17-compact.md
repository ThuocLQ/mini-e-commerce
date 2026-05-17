---
lesson: 17
title: "DiscountService + Strategy Intro"
duration: "90–120 phút"
phase: "Ordering + Checkout + Payment/Webhook Intro"
project: "MicroShop"
testing: "Postman-first"
---

# Buổi 17: DiscountService + Strategy Intro

## 1. Mục tiêu

Bài này tập trung vào việc tạo `DiscountService` cơ bản và hiểu cách dùng **Strategy Pattern** để xử lý nhiều loại mã giảm giá.

Sau bài này, cần làm được:

- Tạo service mới tên `DiscountService`.
- Tạo domain model `Coupon`.
- Tạo API kiểm tra và áp dụng mã giảm giá.
- Hiểu vì sao discount rule không nên viết toàn bộ bằng `if/else` trong endpoint.
- Tạo các discount strategy cơ bản:
  - `PercentageDiscountStrategy`
  - `FixedAmountDiscountStrategy`
- Test được coupon hợp lệ, coupon hết hạn, coupon không tồn tại bằng Postman.
- Chuẩn bị để checkout/payment sau này có thể dùng discount.

Bài này chưa tích hợp sâu `DiscountService` vào checkout để tránh quá tải. Bài này dựng service độc lập trước.

Câu nhớ:

```text
DiscountService không chỉ là CRUD coupon.
DiscountService là nơi đóng gói rule tính giảm giá.
```

---

## 2. Bài này giải quyết vấn đề gì?

Sau Buổi 16, MicroShop đã có checkout baseline:

```text
BasketService có basket
→ OrderingService checkout
→ tạo Order
→ clear basket
```

Nhưng e-commerce thật thường có thêm mã giảm giá:

```text
SAVE10
WELCOME50
FREESHIP
BLACKFRIDAY
```

Nếu cứ nhét rule vào `CheckoutHandler`, code sẽ nhanh rối:

```csharp
if (coupon.Type == "Percentage")
{
    ...
}
else if (coupon.Type == "FixedAmount")
{
    ...
}
else if (coupon.Type == "FreeShipping")
{
    ...
}
```

Khi discount rule tăng lên, `CheckoutHandler` sẽ bị phình to và vi phạm boundary.

Buổi này tách phần đó ra `DiscountService`.

Thiết kế baseline:

| Endpoint | Ý nghĩa |
| --- | --- |
| `GET /discounts/{code}` | Xem thông tin coupon |
| `POST /discounts/apply` | Áp mã giảm giá vào order amount |

Ví dụ:

```text
OrderAmount = 2197
Coupon SAVE10 = giảm 10%
DiscountAmount = 219.7
FinalAmount = 1977.3
```

---

## 3. Ý tưởng chính

### DiscountService chịu trách nhiệm gì?

`DiscountService` chịu trách nhiệm:

```text
Lưu coupon/rule giảm giá.
Kiểm tra coupon có tồn tại không.
Kiểm tra coupon còn hiệu lực không.
Tính discount amount.
Trả kết quả final amount.
```

Trong bài này, service chỉ dùng in-memory repository để học nhanh.

Sau này có thể nâng cấp sang database riêng:

```text
discount.db
PostgreSQL
SQL Server
```

### Vì sao cần Strategy Pattern?

Strategy Pattern giúp tách từng cách tính discount thành từng class riêng.

Thay vì:

```text
Một handler chứa nhiều if/else theo discount type.
```

Ta có:

```text
PercentageDiscountStrategy
FixedAmountDiscountStrategy
```

Mỗi strategy biết cách tính riêng của nó.

Ý tưởng:

```text
Nếu coupon là Percentage → dùng PercentageDiscountStrategy.
Nếu coupon là FixedAmount → dùng FixedAmountDiscountStrategy.
```

Lợi ích:

```text
Dễ thêm loại discount mới.
Rule không bị nhồi vào endpoint/handler.
Code dễ test hơn.
```

### DiscountService chưa phải PaymentService

Không nhầm `DiscountService` với `PaymentService`.

| Service | Trách nhiệm |
| --- | --- |
| `DiscountService` | Tính giảm giá |
| `OrderingService` | Tạo order |
| `PaymentService` | Xử lý thanh toán |
| `BasketService` | Quản lý giỏ hàng |

Bài 18 mới học:

```text
PaymentService + Payment Webhook Intro
```

---

## 4. Flow tổng quan

Flow áp coupon:

```text
Client/Postman
→ POST /discounts/apply
→ DiscountEndpoint
→ ApplyDiscountCommand
→ ApplyDiscountHandler
→ IDiscountRepository.GetByCodeAsync
→ DiscountStrategyFactory
→ IDiscountStrategy.Calculate
→ DiscountResultDto
```

Dependency đúng:

```text
API → Application → Domain
Infrastructure → Application + Domain
Domain không biết API/DB/framework
```

---

## 5. Thực hành

### 5.1. Tạo DiscountService project

Đứng ở root solution `MicroShop`:

```bash
dotnet new webapi -n DiscountService -o Services/DiscountService
dotnet sln add Services/DiscountService/DiscountService.csproj
```

Chạy thử:

```bash
dotnet run --project Services/DiscountService/DiscountService.csproj
```

Port gợi ý:

```text
ApiGateway       5000
CatalogService   5001
BasketService    5002
IdentityService  5003
OrderingService  5004
DiscountService  5005
```

Sửa `Services/DiscountService/Properties/launchSettings.json` nếu cần:

```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5005",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

Kiểm tra nhanh:

```text
[ ] DiscountService build được.
[ ] DiscountService chạy được.
[ ] Service dùng port 5005.
```

---

### 5.2. Cài package cần thiết

Bài này dùng Minimal API + MediatR.

```bash
dotnet add Services/DiscountService/DiscountService.csproj package MediatR
dotnet build Services/DiscountService/DiscountService.csproj
```

Bài này chưa dùng database để tránh loãng. Repository sẽ in-memory.

Kiểm tra nhanh:

```text
[ ] Có MediatR.
[ ] Build pass.
```

---

### 5.3. Tạo folder structure

Tạo cấu trúc:

```text
Services/DiscountService/
├── API/
│   └── Endpoints/
├── Application/
│   ├── Abstractions/
│   └── Discounts/
│       ├── ApplyDiscount/
│       └── GetDiscountByCode/
├── Domain/
│   └── Discounts/
├── Infrastructure/
│   └── Persistence/
├── Program.cs
└── appsettings.Development.json
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force Services/DiscountService/API/Endpoints
New-Item -ItemType Directory -Force Services/DiscountService/Application/Abstractions
New-Item -ItemType Directory -Force Services/DiscountService/Application/Discounts/ApplyDiscount
New-Item -ItemType Directory -Force Services/DiscountService/Application/Discounts/GetDiscountByCode
New-Item -ItemType Directory -Force Services/DiscountService/Domain/Discounts
New-Item -ItemType Directory -Force Services/DiscountService/Infrastructure/Persistence
```

Kiểm tra nhanh:

```text
[ ] Có API/Endpoints.
[ ] Có Application/Discounts.
[ ] Có Domain/Discounts.
[ ] Có Infrastructure/Persistence.
```

---

### 5.4. Tạo Domain model Coupon

Tạo file `Domain/Discounts/DiscountType.cs`:

```csharp
namespace DiscountService.Domain.Discounts;

public enum DiscountType
{
    Percentage = 1,
    FixedAmount = 2
}
```

Tạo file `Domain/Discounts/Coupon.cs`:

```csharp
namespace DiscountService.Domain.Discounts;

public sealed class Coupon
{
    public string Code { get; }
    public DiscountType Type { get; }
    public decimal Value { get; }
    public DateTime ValidFromUtc { get; }
    public DateTime ValidToUtc { get; }
    public bool IsActive { get; }

    public Coupon(
        string code,
        DiscountType type,
        decimal value,
        DateTime validFromUtc,
        DateTime validToUtc,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Coupon code is required.", nameof(code));
        }

        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Coupon value must be greater than zero.");
        }

        if (validToUtc <= validFromUtc)
        {
            throw new ArgumentException("ValidToUtc must be greater than ValidFromUtc.");
        }

        Code = code.Trim().ToUpperInvariant();
        Type = type;
        Value = value;
        ValidFromUtc = validFromUtc;
        ValidToUtc = validToUtc;
        IsActive = isActive;
    }

    public bool CanBeUsedAt(DateTime utcNow)
    {
        return IsActive
            && utcNow >= ValidFromUtc
            && utcNow <= ValidToUtc;
    }
}
```

Điểm cần hiểu:

```text
Coupon tự chuẩn hóa Code về uppercase.
Coupon tự biết mình còn hiệu lực hay không.
Domain không biết HTTP/Postman/Database.
```

---

### 5.5. Tạo Discount Strategy

Tạo file `Domain/Discounts/IDiscountStrategy.cs`:

```csharp
namespace DiscountService.Domain.Discounts;

public interface IDiscountStrategy
{
    DiscountType Type { get; }

    decimal CalculateDiscount(decimal orderAmount, Coupon coupon);
}
```

Tạo file `Domain/Discounts/PercentageDiscountStrategy.cs`:

```csharp
namespace DiscountService.Domain.Discounts;

public sealed class PercentageDiscountStrategy : IDiscountStrategy
{
    public DiscountType Type => DiscountType.Percentage;

    public decimal CalculateDiscount(decimal orderAmount, Coupon coupon)
    {
        if (orderAmount <= 0)
        {
            return 0;
        }

        var percentage = coupon.Value / 100m;
        var discountAmount = orderAmount * percentage;

        return Math.Round(discountAmount, 2);
    }
}
```

Tạo file `Domain/Discounts/FixedAmountDiscountStrategy.cs`:

```csharp
namespace DiscountService.Domain.Discounts;

public sealed class FixedAmountDiscountStrategy : IDiscountStrategy
{
    public DiscountType Type => DiscountType.FixedAmount;

    public decimal CalculateDiscount(decimal orderAmount, Coupon coupon)
    {
        if (orderAmount <= 0)
        {
            return 0;
        }

        return Math.Min(orderAmount, coupon.Value);
    }
}
```

Tạo file `Domain/Discounts/DiscountStrategyFactory.cs`:

```csharp
namespace DiscountService.Domain.Discounts;

public sealed class DiscountStrategyFactory
{
    private readonly IReadOnlyList<IDiscountStrategy> _strategies;

    public DiscountStrategyFactory(IEnumerable<IDiscountStrategy> strategies)
    {
        _strategies = strategies.ToList();
    }

    public IDiscountStrategy GetStrategy(DiscountType type)
    {
        var strategy = _strategies.FirstOrDefault(strategy => strategy.Type == type);

        return strategy
            ?? throw new InvalidOperationException($"Discount strategy for type '{type}' was not found.");
    }
}
```

Điểm cần hiểu:

```text
Handler không cần if/else theo từng discount type.
Handler hỏi factory lấy strategy phù hợp.
```

---

### 5.6. Tạo DTO và repository interface

Tạo file `Application/Discounts/DiscountResultDto.cs`:

```csharp
namespace DiscountService.Application.Discounts;

public sealed record DiscountResultDto(
    string CouponCode,
    bool IsValid,
    decimal OrderAmount,
    decimal DiscountAmount,
    decimal FinalAmount,
    string Message);

public sealed record CouponDto(
    string Code,
    string Type,
    decimal Value,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    bool IsActive);
```

Tạo file `Application/Discounts/DiscountMapper.cs`:

```csharp
using DiscountService.Domain.Discounts;

namespace DiscountService.Application.Discounts;

public static class DiscountMapper
{
    public static CouponDto ToDto(Coupon coupon)
    {
        return new CouponDto(
            coupon.Code,
            coupon.Type.ToString(),
            coupon.Value,
            coupon.ValidFromUtc,
            coupon.ValidToUtc,
            coupon.IsActive);
    }
}
```

Tạo file `Application/Abstractions/IDiscountRepository.cs`:

```csharp
using DiscountService.Domain.Discounts;

namespace DiscountService.Application.Abstractions;

public interface IDiscountRepository
{
    Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}
```

Repository interface nằm trong `Application`. Implementation nằm trong `Infrastructure`.

---

### 5.7. Tạo InMemoryDiscountRepository

Tạo file `Infrastructure/Persistence/InMemoryDiscountRepository.cs`:

```csharp
using DiscountService.Application.Abstractions;
using DiscountService.Domain.Discounts;

namespace DiscountService.Infrastructure.Persistence;

public sealed class InMemoryDiscountRepository : IDiscountRepository
{
    private static readonly IReadOnlyList<Coupon> Coupons =
    [
        new Coupon(
            "SAVE10",
            DiscountType.Percentage,
            10,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(30),
            true),

        new Coupon(
            "WELCOME50",
            DiscountType.FixedAmount,
            50,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(30),
            true),

        new Coupon(
            "EXPIRED20",
            DiscountType.Percentage,
            20,
            DateTime.UtcNow.AddDays(-60),
            DateTime.UtcNow.AddDays(-1),
            true),

        new Coupon(
            "DISABLED15",
            DiscountType.Percentage,
            15,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(30),
            false)
    ];

    public Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();

        var coupon = Coupons.FirstOrDefault(coupon => coupon.Code == normalizedCode);

        return Task.FromResult(coupon);
    }
}
```

Coupon demo:

| Code | Type | Value | Expected |
| --- | --- | --- | --- |
| `SAVE10` | Percentage | 10 | Giảm 10% |
| `WELCOME50` | FixedAmount | 50 | Giảm 50 |
| `EXPIRED20` | Percentage | 20 | Hết hạn |
| `DISABLED15` | Percentage | 15 | Bị disable |

---

### 5.8. Tạo GetDiscountByCode use case

Tạo file `Application/Discounts/GetDiscountByCode/GetDiscountByCodeQuery.cs`:

```csharp
using MediatR;

namespace DiscountService.Application.Discounts.GetDiscountByCode;

public sealed record GetDiscountByCodeQuery(string Code) : IRequest<CouponDto?>;
```

Tạo file `Application/Discounts/GetDiscountByCode/GetDiscountByCodeHandler.cs`:

```csharp
using DiscountService.Application.Abstractions;
using MediatR;

namespace DiscountService.Application.Discounts.GetDiscountByCode;

public sealed class GetDiscountByCodeHandler : IRequestHandler<GetDiscountByCodeQuery, CouponDto?>
{
    private readonly IDiscountRepository _repository;

    public GetDiscountByCodeHandler(IDiscountRepository repository)
    {
        _repository = repository;
    }

    public async Task<CouponDto?> Handle(GetDiscountByCodeQuery request, CancellationToken cancellationToken)
    {
        var coupon = await _repository.GetByCodeAsync(request.Code, cancellationToken);

        return coupon is null
            ? null
            : DiscountMapper.ToDto(coupon);
    }
}
```

---

### 5.9. Tạo ApplyDiscount use case

Tạo file `Application/Discounts/ApplyDiscount/ApplyDiscountCommand.cs`:

```csharp
using MediatR;

namespace DiscountService.Application.Discounts.ApplyDiscount;

public sealed record ApplyDiscountCommand(
    string CouponCode,
    decimal OrderAmount) : IRequest<DiscountResultDto>;
```

Tạo file `Application/Discounts/ApplyDiscount/ApplyDiscountHandler.cs`:

```csharp
using DiscountService.Application.Abstractions;
using DiscountService.Domain.Discounts;
using MediatR;

namespace DiscountService.Application.Discounts.ApplyDiscount;

public sealed class ApplyDiscountHandler : IRequestHandler<ApplyDiscountCommand, DiscountResultDto>
{
    private readonly IDiscountRepository _repository;
    private readonly DiscountStrategyFactory _strategyFactory;

    public ApplyDiscountHandler(
        IDiscountRepository repository,
        DiscountStrategyFactory strategyFactory)
    {
        _repository = repository;
        _strategyFactory = strategyFactory;
    }

    public async Task<DiscountResultDto> Handle(ApplyDiscountCommand request, CancellationToken cancellationToken)
    {
        if (request.OrderAmount <= 0)
        {
            return new DiscountResultDto(
                request.CouponCode,
                false,
                request.OrderAmount,
                0,
                request.OrderAmount,
                "Order amount must be greater than zero.");
        }

        var coupon = await _repository.GetByCodeAsync(request.CouponCode, cancellationToken);

        if (coupon is null)
        {
            return new DiscountResultDto(
                request.CouponCode,
                false,
                request.OrderAmount,
                0,
                request.OrderAmount,
                "Coupon was not found.");
        }

        if (!coupon.CanBeUsedAt(DateTime.UtcNow))
        {
            return new DiscountResultDto(
                coupon.Code,
                false,
                request.OrderAmount,
                0,
                request.OrderAmount,
                "Coupon is expired or inactive.");
        }

        var strategy = _strategyFactory.GetStrategy(coupon.Type);
        var discountAmount = strategy.CalculateDiscount(request.OrderAmount, coupon);
        var finalAmount = Math.Max(0, request.OrderAmount - discountAmount);

        return new DiscountResultDto(
            coupon.Code,
            true,
            request.OrderAmount,
            discountAmount,
            finalAmount,
            "Coupon applied.");
    }
}
```

Điểm cần hiểu:

```text
Handler điều phối use case.
Coupon kiểm tra hiệu lực.
Strategy tính số tiền giảm.
Handler trả result rõ ràng.
```

---

### 5.10. Tạo DiscountEndpoints

Tạo file `API/Endpoints/DiscountEndpoints.cs`:

```csharp
using DiscountService.Application.Discounts.ApplyDiscount;
using DiscountService.Application.Discounts.GetDiscountByCode;
using MediatR;

namespace DiscountService.API.Endpoints;

public static class DiscountEndpoints
{
    public static IEndpointRouteBuilder MapDiscountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/discounts")
            .WithTags("Discounts");

        group.MapGet("/{code}", async (
            string code,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetDiscountByCodeQuery(code), cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.Ok(result);
        });

        group.MapPost("/apply", async (
            ApplyDiscountRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new ApplyDiscountCommand(
                request.CouponCode,
                request.OrderAmount);

            var result = await sender.Send(command, cancellationToken);

            return Results.Ok(result);
        });

        return app;
    }

    private sealed record ApplyDiscountRequest(
        string CouponCode,
        decimal OrderAmount);
}
```

Endpoint chỉ nhận request, map sang command/query, gọi handler.

Không viết rule tính discount trong endpoint.

---

### 5.11. Cấu hình Program.cs

Sửa `Program.cs`:

```csharp
using DiscountService.API.Endpoints;
using DiscountService.Application.Abstractions;
using DiscountService.Domain.Discounts;
using DiscountService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddSingleton<IDiscountRepository, InMemoryDiscountRepository>();

builder.Services.AddSingleton<IDiscountStrategy, PercentageDiscountStrategy>();
builder.Services.AddSingleton<IDiscountStrategy, FixedAmountDiscountStrategy>();
builder.Services.AddSingleton<DiscountStrategyFactory>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDiscountEndpoints();

app.Run();
```

Kiểm tra nhanh:

```text
[ ] Register IDiscountRepository.
[ ] Register PercentageDiscountStrategy.
[ ] Register FixedAmountDiscountStrategy.
[ ] Register DiscountStrategyFactory.
[ ] MapDiscountEndpoints.
```

---

### 5.12. Build và chạy DiscountService

Build:

```bash
dotnet build Services/DiscountService/DiscountService.csproj
```

Chạy:

```bash
dotnet run --project Services/DiscountService/DiscountService.csproj
```

Mở Swagger nếu dùng development:

```text
http://localhost:5005/swagger
```

Kiểm tra nhanh:

```text
[ ] DiscountService build pass.
[ ] DiscountService chạy port 5005.
[ ] Swagger có /discounts/{code}.
[ ] Swagger có /discounts/apply.
```

---

## 6. Test bằng Postman

Tạo hoặc cập nhật environment:

| Variable | Initial value |
| --- | --- |
| `discount_url` | `http://localhost:5005` |
| `coupon_code` | `SAVE10` |
| `order_amount` | `2197` |

Tạo Collection:

```text
MicroShop - Lesson 17 Discount
```

### Danh sách request

| # | Request | Method | URL | Expected |
| --- | --- | --- | --- | --- |
| 1 | Get Coupon SAVE10 | `GET` | `{{discount_url}}/discounts/SAVE10` | `200 OK` |
| 2 | Apply SAVE10 | `POST` | `{{discount_url}}/discounts/apply` | `200 OK`, valid |
| 3 | Apply WELCOME50 | `POST` | `{{discount_url}}/discounts/apply` | `200 OK`, valid |
| 4 | Apply NOTFOUND | `POST` | `{{discount_url}}/discounts/apply` | `200 OK`, invalid |
| 5 | Apply EXPIRED20 | `POST` | `{{discount_url}}/discounts/apply` | `200 OK`, invalid |
| 6 | Apply DISABLED15 | `POST` | `{{discount_url}}/discounts/apply` | `200 OK`, invalid |
| 7 | Apply amount <= 0 | `POST` | `{{discount_url}}/discounts/apply` | `200 OK`, invalid |

### Body apply SAVE10

```json
{
  "couponCode": "SAVE10",
  "orderAmount": 2197
}
```

Expected response:

```json
{
  "couponCode": "SAVE10",
  "isValid": true,
  "orderAmount": 2197,
  "discountAmount": 219.7,
  "finalAmount": 1977.3,
  "message": "Coupon applied."
}
```

### Body apply WELCOME50

```json
{
  "couponCode": "WELCOME50",
  "orderAmount": 2197
}
```

Expected:

```text
discountAmount = 50
finalAmount = 2147
isValid = true
```

### Body coupon không tồn tại

```json
{
  "couponCode": "NOTFOUND",
  "orderAmount": 2197
}
```

Expected:

```text
isValid = false
discountAmount = 0
finalAmount = 2197
message = Coupon was not found.
```

Kiểm tra nhanh:

```text
[ ] SAVE10 giảm đúng 10%.
[ ] WELCOME50 giảm đúng 50.
[ ] NOTFOUND invalid.
[ ] EXPIRED20 invalid.
[ ] DISABLED15 invalid.
[ ] Amount <= 0 invalid.
```

---

## 7. Checklist hoàn thành

```text
[ ] Tạo được DiscountService.
[ ] DiscountService chạy port 5005.
[ ] Có folder API/Application/Domain/Infrastructure.
[ ] Có DiscountType enum.
[ ] Có Coupon domain model.
[ ] Có IDiscountStrategy.
[ ] Có PercentageDiscountStrategy.
[ ] Có FixedAmountDiscountStrategy.
[ ] Có DiscountStrategyFactory.
[ ] Có DiscountResultDto và CouponDto.
[ ] Có DiscountMapper.
[ ] Có IDiscountRepository.
[ ] Có InMemoryDiscountRepository.
[ ] Có GetDiscountByCodeQuery/Handler.
[ ] Có ApplyDiscountCommand/Handler.
[ ] Có DiscountEndpoints.
[ ] Program.cs register đầy đủ dependency.
[ ] GET /discounts/{code} chạy được.
[ ] POST /discounts/apply chạy được.
[ ] Postman test được coupon hợp lệ.
[ ] Postman test được coupon invalid/expired/disabled.
[ ] Giải thích được Strategy Pattern giúp gì trong bài này.
```

---

## 8. Bài tập

### Bài 1

Vẽ flow apply coupon:

```text
POST /discounts/apply
→ DiscountEndpoint
→ ApplyDiscountCommand
→ ApplyDiscountHandler
→ IDiscountRepository
→ Coupon
→ DiscountStrategyFactory
→ IDiscountStrategy
→ DiscountResultDto
```

### Bài 2

Test bằng Postman và ghi lại kết quả:

| Coupon | OrderAmount | Expected | Actual |
| --- | --- | --- | --- |
| `SAVE10` | `2197` | Giảm 219.7 | |
| `WELCOME50` | `2197` | Giảm 50 | |
| `NOTFOUND` | `2197` | Invalid | |
| `EXPIRED20` | `2197` | Invalid | |
| `DISABLED15` | `2197` | Invalid | |

### Bài 3

Tự giải thích:

```text
Vì sao không nên viết toàn bộ discount rule bằng if/else trong endpoint?
```

Gợi ý:

```text
Endpoint nên mỏng.
Rule tính discount nên nằm trong Domain/Application.
Strategy giúp tách từng thuật toán giảm giá ra class riêng.
```

### Bài 4

Tự thêm coupon mới vào `InMemoryDiscountRepository`:

```text
VIP20
Percentage
20%
```

Sau đó test:

```json
{
  "couponCode": "VIP20",
  "orderAmount": 1000
}
```

Expected:

```text
discountAmount = 200
finalAmount = 800
```

### Bài 5

Design question:

```text
Sau này checkout nên gọi DiscountService ở bước nào?
```

Gợi ý:

```text
Trước khi tạo order final hoặc trước khi payment.
Cần tính finalAmount rõ ràng để order/payment dùng cùng một kết quả.
```

---

## 9. Quiz / Review

**Câu 1. DiscountService chịu trách nhiệm chính việc gì?**

```text
A. Thanh toán
B. Tính và kiểm tra mã giảm giá
C. Lưu giỏ hàng
```

Đáp án: B

**Câu 2. Strategy Pattern giúp gì trong bài này?**

```text
A. Tách từng cách tính discount thành class riêng
B. Tạo JWT token
C. Kết nối Redis
```

Đáp án: A

**Câu 3. Coupon hết hạn nên trả kết quả thế nào?**

```text
A. isValid = true, discountAmount > 0
B. isValid = false, discountAmount = 0
C. Tự động tạo coupon mới
```

Đáp án: B

**Câu 4. Endpoint có nên chứa toàn bộ rule tính discount không?**

```text
A. Có, để code nằm một chỗ
B. Không, endpoint nên mỏng và gọi use case
C. Có, vì strategy không cần thiết
```

Đáp án: B

Review cuối bài:

```text
Bài 17 giúp MicroShop tiến gần checkout/payment thật hơn ở điểm nào?
```

Gợi ý:

```text
Trước bài 17, checkout chưa có concept discount.
Sau bài 17, MicroShop có DiscountService độc lập để tính coupon/discount rule, chuẩn bị tích hợp vào checkout/payment sau này.
```

---

## 10. Chưa học trong bài này

Chưa học sâu:

```text
[ ] Tích hợp DiscountService vào CheckoutHandler.
[ ] Lưu coupon bằng database riêng.
[ ] Coupon usage limit.
[ ] Coupon per user.
[ ] Minimum order amount.
[ ] Free shipping.
[ ] Stacking nhiều coupon.
[ ] Distributed consistency giữa discount/order/payment.
[ ] Idempotency khi apply coupon.
[ ] Audit log coupon usage.
[ ] Promotion campaign phức tạp.
```

Lý do:

```text
Buổi 17 chỉ tạo DiscountService baseline và Strategy Pattern mindset.
Nếu nhồi toàn bộ promotion engine vào đây sẽ quá tải.
```

---

## 11. Học phần nâng cao ở đâu?

| Chủ đề | Học ở đâu |
| --- | --- |
| Tích hợp discount vào checkout | Các bài checkout/payment nâng cấp |
| Payment flow | Buổi 18 |
| Outbox basic | Buổi 23 |
| Idempotency basic | Buổi 22 |
| Distributed consistency | Buổi 42 |
| Saga | Buổi 43/44 |
| Timeout/Retry/Circuit Breaker | Buổi 45 |
| Promotion engine phức tạp | Sau Stage 1 hoặc project extension |

Production mindset cần nhớ:

```text
Discount có thể ảnh hưởng trực tiếp đến tiền.
Production cần audit, validation, usage limit, idempotency và consistency với order/payment.
```

---

## Điều kiện mở khóa Buổi 18

Bạn có thể sang Buổi 18 khi:

```text
[ ] DiscountService chạy được.
[ ] GET /discounts/{code} lấy được coupon.
[ ] POST /discounts/apply tính đúng SAVE10.
[ ] POST /discounts/apply tính đúng WELCOME50.
[ ] Coupon không tồn tại trả invalid.
[ ] Coupon expired/disabled trả invalid.
[ ] Bạn giải thích được Strategy Pattern trong bài này.
[ ] Bạn hiểu bài này chưa phải promotion engine production.
```

Buổi 18:

```text
PaymentService + Payment Webhook Intro
```

Mục tiêu Buổi 18:

```text
Tạo PaymentService giả lập thanh toán success/fail và tạo endpoint webhook demo để hiểu webhook khác RabbitMQ/Kafka thế nào.
```
