---
lesson: 18
title: "PaymentService + Payment Webhook Intro"
duration: "90–120 phút"
phase: "Ordering + Checkout + Payment/Webhook Intro"
project: "MicroShop"
testing: "Postman-first"
---

# Buổi 18: PaymentService + Payment Webhook Intro

## 1. Mục tiêu

Bài này tạo `PaymentService` bản baseline để mô phỏng thanh toán và hiểu webhook ở mức nhập môn.

Sau bài này, cần làm được:

```text
[ ] Tạo PaymentService mới.
[ ] Tạo domain model Payment.
[ ] Tạo API giả lập thanh toán success/fail.
[ ] Tạo endpoint webhook nhận kết quả từ payment provider giả lập.
[ ] Hiểu webhook khác REST request thường, RabbitMQ và Kafka như thế nào.
[ ] Test payment flow bằng Postman.
[ ] Chuẩn bị cho các bài reliability/event-driven sau này.
```

Câu nhớ:

```text
Webhook là HTTP callback từ hệ thống bên ngoài gọi ngược vào hệ thống của mình.
```

Bài này chưa làm payment production. Không xử lý tiền thật, không tích hợp VNPay/Stripe/Momo, không xử lý signature chuẩn production, không làm saga/outbox/idempotency sâu.

---

## 2. Bài này giải quyết vấn đề gì?

Sau Buổi 15–17, MicroShop đã có:

```text
OrderingService
Checkout Flow
DiscountService
```

Nhưng e-commerce thật còn cần payment:

```text
User checkout
→ hệ thống tạo order
→ user thanh toán
→ payment provider báo kết quả
→ order/payment status được cập nhật
```

Trong thực tế, payment provider thường không chỉ trả kết quả ngay lập tức. Có nhiều trường hợp:

```text
Thanh toán đang pending.
Ngân hàng xử lý chậm.
Provider gọi lại sau vài giây/phút.
Provider retry callback nếu hệ thống mình lỗi.
Một callback có thể bị gửi nhiều lần.
```

Đó là lý do cần biết **webhook**.

Bài này tạo baseline:

| API | Ý nghĩa |
| --- | --- |
| `POST /payments` | Tạo payment giả lập cho order |
| `GET /payments/{id}` | Xem payment detail |
| `POST /webhooks/payment` | Nhận kết quả payment provider gọi về |

---

## 3. Ý tưởng chính

### PaymentService chịu trách nhiệm gì?

Trong MicroShop, `PaymentService` chịu trách nhiệm:

```text
Tạo payment record.
Theo dõi trạng thái payment.
Nhận webhook từ provider.
Cập nhật payment status.
```

Không nên nhét payment logic vào `OrderingService`.

| Service | Trách nhiệm |
| --- | --- |
| `OrderingService` | Tạo và quản lý order |
| `DiscountService` | Tính discount |
| `PaymentService` | Theo dõi và xử lý payment |
| `BasketService` | Quản lý basket |

### Webhook là gì?

Webhook là HTTP request do hệ thống bên ngoài chủ động gọi vào hệ thống mình.

Ví dụ:

```text
Payment Provider
→ POST /webhooks/payment
→ MicroShop PaymentService
```

Khác với request thường:

```text
Client/Postman
→ POST /payments
```

Webhook thường cần quan tâm:

```text
Security
Signature
Idempotency
Retry
Duplicate event
Webhook log
Order/payment consistency
```

Bài này chỉ intro, chưa làm full production.

### Webhook khác RabbitMQ/Kafka thế nào?

| Cơ chế | Bản chất |
| --- | --- |
| REST thường | Mình gọi service khác và chờ response |
| Webhook | Hệ thống ngoài gọi ngược vào mình qua HTTP |
| RabbitMQ | Message broker queue/event nội bộ, consumer xử lý async |
| Kafka | Event streaming/log, phù hợp projection/analytics/stream |

Câu nhớ:

```text
Webhook vẫn là HTTP.
RabbitMQ/Kafka là messaging infrastructure.
```

---

## 4. Flow tổng quan

Payment baseline flow:

```text
Client/Postman
→ POST /payments
→ CreatePaymentCommand
→ CreatePaymentHandler
→ Payment domain model
→ IPaymentRepository.CreateAsync
→ PaymentDto
```

Webhook demo flow:

```text
Payment Provider giả lập/Postman
→ POST /webhooks/payment
→ PaymentWebhookCommand
→ PaymentWebhookHandler
→ IPaymentRepository.GetByIdAsync
→ Payment.MarkSucceeded / Payment.MarkFailed
→ IPaymentRepository.UpdateAsync
→ Webhook accepted
```

Trạng thái payment trong bài:

```text
Pending
Succeeded
Failed
```

---

## 5. Thực hành

### 5.1. Tạo PaymentService project

Đứng ở root solution `MicroShop`:

```bash
dotnet new webapi -n PaymentService -o Services/PaymentService
dotnet sln add Services/PaymentService/PaymentService.csproj
```

Port gợi ý:

```text
ApiGateway       5000
CatalogService   5001
BasketService    5002
IdentityService  5003
OrderingService  5004
DiscountService  5005
PaymentService   5006
```

Sửa `Services/PaymentService/Properties/launchSettings.json`:

```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5006",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

Kiểm tra:

```bash
dotnet run --project Services/PaymentService/PaymentService.csproj
```

---

### 5.2. Cài package cần thiết

Bài này dùng Minimal API + MediatR + in-memory repository.

```bash
dotnet add Services/PaymentService/PaymentService.csproj package MediatR
dotnet build Services/PaymentService/PaymentService.csproj
```

Bài này chưa dùng database để tránh loãng. Sau này sẽ nâng cấp persistence, webhook log, inbox/idempotency.

---

### 5.3. Tạo folder structure

Tạo cấu trúc:

```text
Services/PaymentService/
├── API/
│   └── Endpoints/
├── Application/
│   ├── Abstractions/
│   └── Payments/
│       ├── CreatePayment/
│       ├── GetPaymentById/
│       └── Webhooks/
├── Domain/
│   └── Payments/
├── Infrastructure/
│   └── Persistence/
├── Program.cs
└── appsettings.Development.json
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force Services/PaymentService/API/Endpoints
New-Item -ItemType Directory -Force Services/PaymentService/Application/Abstractions
New-Item -ItemType Directory -Force Services/PaymentService/Application/Payments/CreatePayment
New-Item -ItemType Directory -Force Services/PaymentService/Application/Payments/GetPaymentById
New-Item -ItemType Directory -Force Services/PaymentService/Application/Payments/Webhooks
New-Item -ItemType Directory -Force Services/PaymentService/Domain/Payments
New-Item -ItemType Directory -Force Services/PaymentService/Infrastructure/Persistence
```

---

### 5.4. Tạo Payment domain model

Tạo file `Domain/Payments/PaymentStatus.cs`:

```csharp
namespace PaymentService.Domain.Payments;

public enum PaymentStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3
}
```

Tạo file `Domain/Payments/Payment.cs`:

```csharp
namespace PaymentService.Domain.Payments;

public sealed class Payment
{
    public Guid Id { get; }
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public PaymentStatus Status { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; }
    public DateTime? CompletedAtUtc { get; private set; }

    public Payment(
        Guid id,
        Guid orderId,
        Guid customerId,
        decimal amount,
        string currency,
        PaymentStatus status,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Payment id cannot be empty.", nameof(id));
        if (orderId == Guid.Empty) throw new ArgumentException("Order id cannot be empty.", nameof(orderId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer id cannot be empty.", nameof(customerId));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency is required.", nameof(currency));

        Id = id;
        OrderId = orderId;
        CustomerId = customerId;
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkSucceeded(string providerTransactionId, DateTime completedAtUtc)
    {
        if (Status == PaymentStatus.Succeeded)
        {
            return;
        }

        if (Status == PaymentStatus.Failed)
        {
            throw new InvalidOperationException("Failed payment cannot be marked as succeeded.");
        }

        if (string.IsNullOrWhiteSpace(providerTransactionId))
        {
            throw new ArgumentException("Provider transaction id is required.", nameof(providerTransactionId));
        }

        Status = PaymentStatus.Succeeded;
        ProviderTransactionId = providerTransactionId.Trim();
        FailureReason = null;
        CompletedAtUtc = completedAtUtc;
    }

    public void MarkFailed(string reason, DateTime completedAtUtc)
    {
        if (Status == PaymentStatus.Failed)
        {
            return;
        }

        if (Status == PaymentStatus.Succeeded)
        {
            throw new InvalidOperationException("Succeeded payment cannot be marked as failed.");
        }

        Status = PaymentStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Payment failed." : reason.Trim();
        CompletedAtUtc = completedAtUtc;
    }
}
```

Điểm cần nhớ:

```text
Payment tự bảo vệ transition cơ bản.
Pending có thể chuyển thành Succeeded hoặc Failed.
Succeeded không nên chuyển ngược thành Failed trong baseline.
Failed không nên chuyển ngược thành Succeeded trong baseline.
```

---

### 5.5. Tạo DTO, mapper và repository interface

Tạo file `Application/Payments/PaymentDto.cs`:

```csharp
namespace PaymentService.Application.Payments;

public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string Status,
    string? ProviderTransactionId,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);
```

Tạo file `Application/Payments/PaymentMapper.cs`:

```csharp
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments;

public static class PaymentMapper
{
    public static PaymentDto ToDto(Payment payment)
    {
        return new PaymentDto(
            payment.Id,
            payment.OrderId,
            payment.CustomerId,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            payment.ProviderTransactionId,
            payment.FailureReason,
            payment.CreatedAtUtc,
            payment.CompletedAtUtc);
    }
}
```

Tạo file `Application/Abstractions/IPaymentRepository.cs`:

```csharp
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Abstractions;

public interface IPaymentRepository
{
    Task<Payment> CreateAsync(Payment payment, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Payment payment, CancellationToken cancellationToken = default);
}
```

---

### 5.6. Tạo InMemoryPaymentRepository

Tạo file `Infrastructure/Persistence/InMemoryPaymentRepository.cs`:

```csharp
using System.Collections.Concurrent;
using PaymentService.Application.Abstractions;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence;

public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public Task<Payment> CreateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        _payments[payment.Id] = payment;
        return Task.FromResult(payment);
    }

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _payments.TryGetValue(id, out var payment);
        return Task.FromResult(payment);
    }

    public Task<bool> UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (!_payments.ContainsKey(payment.Id))
        {
            return Task.FromResult(false);
        }

        _payments[payment.Id] = payment;
        return Task.FromResult(true);
    }
}
```

Lý do dùng in-memory:

```text
Bài này tập trung vào flow payment/webhook.
Database, webhook log và idempotency sẽ học sau.
```

---

### 5.7. Tạo CreatePayment use case

Tạo file `Application/Payments/CreatePayment/CreatePaymentCommand.cs`:

```csharp
using MediatR;

namespace PaymentService.Application.Payments.CreatePayment;

public sealed record CreatePaymentCommand(
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency) : IRequest<PaymentDto>;
```

Tạo file `Application/Payments/CreatePayment/CreatePaymentHandler.cs`:

```csharp
using MediatR;
using PaymentService.Application.Abstractions;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.CreatePayment;

public sealed class CreatePaymentHandler : IRequestHandler<CreatePaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _repository;

    public CreatePaymentHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaymentDto> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = new Payment(
            Guid.NewGuid(),
            request.OrderId,
            request.CustomerId,
            request.Amount,
            request.Currency,
            PaymentStatus.Pending,
            DateTime.UtcNow);

        var createdPayment = await _repository.CreateAsync(payment, cancellationToken);

        return PaymentMapper.ToDto(createdPayment);
    }
}
```

Ý nghĩa:

```text
POST /payments tạo payment ở trạng thái Pending.
Webhook sẽ cập nhật Succeeded/Failed sau.
```

---

### 5.8. Tạo GetPaymentById use case

Tạo file `Application/Payments/GetPaymentById/GetPaymentByIdQuery.cs`:

```csharp
using MediatR;

namespace PaymentService.Application.Payments.GetPaymentById;

public sealed record GetPaymentByIdQuery(Guid Id) : IRequest<PaymentDto?>;
```

Tạo file `Application/Payments/GetPaymentById/GetPaymentByIdHandler.cs`:

```csharp
using MediatR;
using PaymentService.Application.Abstractions;

namespace PaymentService.Application.Payments.GetPaymentById;

public sealed class GetPaymentByIdHandler : IRequestHandler<GetPaymentByIdQuery, PaymentDto?>
{
    private readonly IPaymentRepository _repository;

    public GetPaymentByIdHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaymentDto?> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await _repository.GetByIdAsync(request.Id, cancellationToken);

        return payment is null
            ? null
            : PaymentMapper.ToDto(payment);
    }
}
```

---

### 5.9. Tạo Payment Webhook use case

Tạo file `Application/Payments/Webhooks/PaymentWebhookCommand.cs`:

```csharp
using MediatR;

namespace PaymentService.Application.Payments.Webhooks;

public sealed record PaymentWebhookCommand(
    Guid PaymentId,
    string ProviderTransactionId,
    string Status,
    string? FailureReason) : IRequest<PaymentDto?>;
```

Tạo file `Application/Payments/Webhooks/PaymentWebhookHandler.cs`:

```csharp
using MediatR;
using PaymentService.Application.Abstractions;

namespace PaymentService.Application.Payments.Webhooks;

public sealed class PaymentWebhookHandler : IRequestHandler<PaymentWebhookCommand, PaymentDto?>
{
    private readonly IPaymentRepository _repository;

    public PaymentWebhookHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaymentDto?> Handle(PaymentWebhookCommand request, CancellationToken cancellationToken)
    {
        var payment = await _repository.GetByIdAsync(request.PaymentId, cancellationToken);

        if (payment is null)
        {
            return null;
        }

        var normalizedStatus = request.Status.Trim().ToUpperInvariant();

        if (normalizedStatus == "SUCCEEDED")
        {
            payment.MarkSucceeded(request.ProviderTransactionId, DateTime.UtcNow);
        }
        else if (normalizedStatus == "FAILED")
        {
            payment.MarkFailed(request.FailureReason ?? "Payment failed by provider.", DateTime.UtcNow);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported payment webhook status '{request.Status}'.");
        }

        await _repository.UpdateAsync(payment, cancellationToken);

        return PaymentMapper.ToDto(payment);
    }
}
```

Điểm cần nhớ:

```text
Webhook handler không tạo payment mới.
Webhook handler tìm payment đang có và cập nhật status.
```

---

### 5.10. Tạo PaymentEndpoints

Tạo file `API/Endpoints/PaymentEndpoints.cs`:

```csharp
using MediatR;
using PaymentService.Application.Payments.CreatePayment;
using PaymentService.Application.Payments.GetPaymentById;

namespace PaymentService.API.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments")
            .WithTags("Payments");

        group.MapPost("/", async (
            CreatePaymentRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CreatePaymentCommand(
                request.OrderId,
                request.CustomerId,
                request.Amount,
                request.Currency);

            var result = await sender.Send(command, cancellationToken);

            return Results.Created($"/payments/{result.Id}", result);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetPaymentByIdQuery(id), cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.Ok(result);
        });

        return app;
    }

    private sealed record CreatePaymentRequest(
        Guid OrderId,
        Guid CustomerId,
        decimal Amount,
        string Currency);
}
```

---

### 5.11. Tạo WebhookEndpoints

Tạo file `API/Endpoints/WebhookEndpoints.cs`:

```csharp
using MediatR;
using PaymentService.Application.Payments.Webhooks;

namespace PaymentService.API.Endpoints;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/webhooks")
            .WithTags("Webhooks");

        group.MapPost("/payment", async (
            PaymentWebhookRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var command = new PaymentWebhookCommand(
                    request.PaymentId,
                    request.ProviderTransactionId,
                    request.Status,
                    request.FailureReason);

                var result = await sender.Send(command, cancellationToken);

                return result is null
                    ? Results.NotFound(new { Error = "Payment was not found." })
                    : Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        return app;
    }

    private sealed record PaymentWebhookRequest(
        Guid PaymentId,
        string ProviderTransactionId,
        string Status,
        string? FailureReason);
}
```

Bài này để webhook public để dễ test bằng Postman. Production phải có signature verification, rate limit, log, idempotency.

---

### 5.12. Cấu hình Program.cs

Sửa `Program.cs`:

```csharp
using PaymentService.API.Endpoints;
using PaymentService.Application.Abstractions;
using PaymentService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddSingleton<IPaymentRepository, InMemoryPaymentRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPaymentEndpoints();
app.MapWebhookEndpoints();

app.Run();
```

Kiểm tra:

```text
[ ] Register IPaymentRepository.
[ ] Register MediatR.
[ ] MapPaymentEndpoints.
[ ] MapWebhookEndpoints.
```

---

### 5.13. Build và chạy PaymentService

Build:

```bash
dotnet build Services/PaymentService/PaymentService.csproj
```

Chạy:

```bash
dotnet run --project Services/PaymentService/PaymentService.csproj
```

Mở Swagger:

```text
http://localhost:5006/swagger
```

---

## 6. Test bằng Postman

Tạo hoặc cập nhật environment:

| Variable | Initial value |
| --- | --- |
| `payment_url` | `http://localhost:5006` |
| `order_id` | `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa` |
| `customer_id` | `11111111-1111-1111-1111-111111111111` |
| `payment_id` | để trống |

Tạo Collection:

```text
MicroShop - Lesson 18 Payment
```

### Danh sách request

| # | Request | Method | URL | Expected |
| --- | --- | --- | --- | --- |
| 1 | Create Payment | `POST` | `{{payment_url}}/payments` | `201 Created`, status `Pending` |
| 2 | Get Payment | `GET` | `{{payment_url}}/payments/{{payment_id}}` | `200 OK` |
| 3 | Webhook Success | `POST` | `{{payment_url}}/webhooks/payment` | `200 OK`, status `Succeeded` |
| 4 | Get Payment After Success | `GET` | `{{payment_url}}/payments/{{payment_id}}` | `200 OK`, status `Succeeded` |
| 5 | Webhook Failed invalid transition | `POST` | `{{payment_url}}/webhooks/payment` | `400 Bad Request` |
| 6 | Webhook Payment Not Found | `POST` | `{{payment_url}}/webhooks/payment` | `404 Not Found` |

### Request 1: Create Payment

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

### Request 3: Webhook Success

```json
{
  "paymentId": "{{payment_id}}",
  "providerTransactionId": "txn_demo_001",
  "status": "SUCCEEDED",
  "failureReason": null
}
```

Expected:

```text
HTTP 200 OK
status = Succeeded
providerTransactionId = txn_demo_001
completedAtUtc có giá trị
```

### Request failed webhook

Tạo payment mới trước nếu muốn test failed transition sạch.

```json
{
  "paymentId": "{{payment_id}}",
  "providerTransactionId": "txn_demo_002",
  "status": "FAILED",
  "failureReason": "Insufficient funds"
}
```

Nếu payment đang `Pending`, expected:

```text
status = Failed
failureReason = Insufficient funds
```

Nếu payment đã `Succeeded`, expected:

```text
400 Bad Request
```

---

## 7. Checklist hoàn thành

```text
[ ] Tạo được PaymentService.
[ ] PaymentService chạy port 5006.
[ ] Có folder API/Application/Domain/Infrastructure.
[ ] Có PaymentStatus enum.
[ ] Có Payment domain model.
[ ] Có PaymentDto và PaymentMapper.
[ ] Có IPaymentRepository.
[ ] Có InMemoryPaymentRepository.
[ ] Có CreatePaymentCommand/Handler.
[ ] Có GetPaymentByIdQuery/Handler.
[ ] Có PaymentWebhookCommand/Handler.
[ ] Có PaymentEndpoints.
[ ] Có WebhookEndpoints.
[ ] Program.cs register đầy đủ dependency.
[ ] POST /payments tạo payment Pending.
[ ] GET /payments/{id} xem được payment.
[ ] POST /webhooks/payment cập nhật Succeeded.
[ ] POST /webhooks/payment cập nhật Failed.
[ ] Test được payment not found.
[ ] Giải thích được webhook khác RabbitMQ/Kafka.
```

---

## 8. Bài tập

### Bài 1

Vẽ flow payment:

```text
POST /payments
→ PaymentEndpoint
→ CreatePaymentCommand
→ CreatePaymentHandler
→ Payment domain
→ IPaymentRepository
→ PaymentDto
```

### Bài 2

Vẽ flow webhook:

```text
POST /webhooks/payment
→ WebhookEndpoint
→ PaymentWebhookCommand
→ PaymentWebhookHandler
→ Get payment
→ MarkSucceeded/MarkFailed
→ Update payment
→ PaymentDto
```

### Bài 3

Test bằng Postman và ghi lại kết quả:

| Case | Expected | Actual |
| --- | --- | --- |
| Create payment | Pending | |
| Webhook success | Succeeded | |
| Webhook failed trên payment mới | Failed | |
| Webhook payment not found | 404 | |
| Webhook status không hỗ trợ | 400 | |

### Bài 4

Tự giải thích:

```text
Webhook khác REST request thường ở điểm nào?
```

Gợi ý:

```text
REST thường là mình gọi service khác.
Webhook là service bên ngoài gọi ngược vào mình khi có sự kiện/kết quả.
```

### Bài 5

Design question:

```text
Vì sao production webhook cần idempotency?
```

Gợi ý:

```text
Provider có thể retry cùng một webhook nhiều lần.
Nếu không idempotent, hệ thống có thể update sai, gửi event trùng hoặc xử lý payment nhiều lần.
```

---

## 9. Quiz / Review

**Câu 1. Webhook là gì?**

```text
A. Message trong Kafka
B. HTTP callback từ hệ thống bên ngoài gọi vào hệ thống của mình
C. Database table
```

Đáp án: B

**Câu 2. Payment mới tạo trong bài này có status gì?**

```text
A. Pending
B. Succeeded
C. Cancelled
```

Đáp án: A

**Câu 3. PaymentService chịu trách nhiệm chính việc gì?**

```text
A. Tính discount
B. Quản lý payment và nhận webhook payment
C. Lưu basket
```

Đáp án: B

**Câu 4. Vì sao webhook production cần idempotency?**

```text
A. Vì webhook có thể bị gửi nhiều lần
B. Vì JWT luôn hết hạn
C. Vì database không cần update
```

Đáp án: A

Review cuối bài:

```text
Bài 18 giúp MicroShop tiến gần hệ thống e-commerce thật hơn ở điểm nào?
```

Gợi ý:

```text
Trước bài 18, MicroShop có checkout/discount nhưng chưa có payment.
Sau bài 18, hệ thống có PaymentService baseline và webhook endpoint để mô phỏng provider callback.
```

---

## 10. Chưa học trong bài này

Chưa học sâu:

```text
[ ] Tích hợp PaymentService vào CheckoutHandler.
[ ] Payment provider thật như Stripe/Momo/VNPay.
[ ] Webhook signature verification.
[ ] Webhook log / Inbox pattern.
[ ] Idempotency key cho webhook.
[ ] Retry/DLQ.
[ ] Outbox event PaymentSucceeded/PaymentFailed.
[ ] Saga giữa Order/Payment/Inventory.
[ ] Update Order status sau payment.
[ ] Refund/cancel payment.
[ ] PCI/security/payment compliance.
```

Lý do:

```text
Buổi 18 chỉ tạo PaymentService baseline và webhook intro.
Nếu nhồi production payment vào đây sẽ quá tải.
```

---

## 11. Học phần nâng cao ở đâu?

| Chủ đề | Học ở đâu |
| --- | --- |
| Retry/DLQ + Idempotency basic | Buổi 22 |
| Outbox basic | Buổi 23 |
| Payment status event | Các bài event-driven |
| Transactional Outbox chuẩn | Buổi 38 |
| Inbox/WebhookLog | Buổi 39 |
| Saga orchestration | Buổi 43 |
| Webhook production handling | Buổi 44 |
| Timeout/Retry/Circuit Breaker | Buổi 45 |

Production mindset cần nhớ:

```text
Payment là flow nhạy cảm vì liên quan đến tiền.
Production cần signature verification, idempotency, audit log, webhook log, retry strategy và consistency với Order.
```

---

## Điều kiện mở khóa Checkpoint 4

Bạn có thể sang Checkpoint 4 khi:

```text
[ ] OrderingService tạo được order.
[ ] Checkout flow Basket → Order chạy được.
[ ] DiscountService apply coupon chạy được.
[ ] PaymentService tạo payment Pending được.
[ ] Payment webhook cập nhật Succeeded/Failed được.
[ ] Bạn giải thích được webhook khác REST/RabbitMQ/Kafka.
[ ] Bạn nêu được vì sao payment webhook cần idempotency.
```

Tiếp theo:

```text
Checkpoint 4: Ordering + Checkout + Discount + Payment/Webhook Review
```

Mục tiêu Checkpoint 4:

```text
Review toàn bộ Phase 1.4, chạy regression test bằng Postman, viết demo script, ghi lại boundary Order/Discount/Payment và chuẩn bị sang Phase 1.5 RabbitMQ + Reliability.
```
