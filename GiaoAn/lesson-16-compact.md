---
lesson: 16
title: "Checkout Flow: Basket → Order"
duration: "90–120 phút"
phase: "Checkout Foundation"
project: "MicroShop"
testing: "Postman-first"
---

# Buổi 16: Checkout Flow — Basket → Order

## 1. Mục tiêu

Bài này tập trung vào flow checkout cơ bản:

```text
BasketService có giỏ hàng
→ Client gọi OrderingService checkout
→ OrderingService lấy basket
→ Tạo Order
→ Clear basket
```

Sau bài này, cần làm được:

- Hiểu checkout khác tạo order thủ công ở bài 15 thế nào.
- Thêm `IBasketClient` để `OrderingService` gọi sang `BasketService`.
- Tạo use case `CheckoutCommand`.
- Tạo endpoint `POST /checkout`.
- Tạo order từ basket items.
- Clear basket sau khi tạo order thành công.
- Test toàn bộ flow bằng Postman.

Bài này chưa làm production checkout đầy đủ như payment, inventory, idempotency, outbox hay saga.

Câu nhớ:

```text
Checkout là orchestration flow.
OrderingService điều phối flow, nhưng không sở hữu dữ liệu Basket.
```

---

## 2. Bài này giải quyết vấn đề gì?

Buổi 15 đã tạo được order bằng request body:

```http
POST /orders
```

Body phải gửi sẵn `customerId` và `items`. Cách này tốt để học `OrderingService`, nhưng chưa giống e-commerce thật.

Thực tế user sẽ:

```text
1. Thêm sản phẩm vào basket.
2. Bấm checkout.
3. Hệ thống lấy basket hiện tại.
4. Tạo order từ basket.
5. Xóa basket sau khi checkout thành công.
```

Thiết kế endpoint mới:

| Endpoint | Service | Ý nghĩa |
| --- | --- | --- |
| `POST /checkout` | `OrderingService` | Tạo order từ basket |
| `GET /baskets/{customerId}` | `BasketService` | Lấy basket của customer |
| `DELETE /baskets/{customerId}` | `BasketService` | Clear basket sau checkout |

Kết quả mong muốn:

| Case | Kết quả |
| --- | --- |
| Basket có items | `201 Created`, tạo order |
| Basket rỗng hoặc không tồn tại | `400 Bad Request` |
| Tạo order thành công | Basket được clear |
| Xem lại order | `GET /orders/{id}` trả đúng order |

---

## 3. Ý tưởng chính

### Checkout là orchestration

`OrderingService` sẽ điều phối các bước:

```text
1. Nhận customerId.
2. Gọi BasketService lấy basket.
3. Kiểm tra basket có item.
4. Tạo Order domain model.
5. Lưu order vào Ordering DB.
6. Gọi BasketService clear basket.
7. Trả order về client.
```

### Vì sao OrderingService không query DB của BasketService?

Không nên:

```text
OrderingService → Redis/database của BasketService
```

Vì như vậy phá nguyên tắc service ownership.

Đúng hơn:

```text
OrderingService → BasketService API
```

Mỗi service sở hữu dữ liệu của mình:

| Data | Owner |
| --- | --- |
| Products | `CatalogService` |
| Basket | `BasketService` |
| Orders | `OrderingService` |

### Checkout baseline có rủi ro gì?

Flow bài này là baseline để học:

```text
Create order thành công
→ Clear basket thất bại
```

Khi đó hệ thống có thể bị lệch trạng thái: order đã tạo nhưng basket vẫn còn, user có thể checkout lại.

Sau này sẽ học:

```text
Idempotency
Outbox pattern
Event-driven OrderCreated
Saga / process manager
Retry policy
Compensation
```

---

## 4. Flow tổng quan

```text
Client
→ POST /checkout
→ CheckoutEndpoint
→ CheckoutCommand
→ CheckoutHandler
→ IBasketClient.GetBasketAsync(customerId)
→ Order domain model
→ IOrderRepository.CreateAsync(order)
→ IBasketClient.ClearBasketAsync(customerId)
→ OrderDto
```

Dependency đúng:

```text
Application định nghĩa IBasketClient.
Infrastructure implement HttpBasketClient.
Handler phụ thuộc IBasketClient, không phụ thuộc HttpClient trực tiếp.
```

---

## 5. Thực hành

### 5.1. Thêm config URL của BasketService

Sửa `Services/OrderingService/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "OrderingDb": "Data Source=ordering.db"
  },
  "Services": {
    "BasketServiceBaseUrl": "http://localhost:5002"
  }
}
```

Nếu `BasketService` của bạn dùng port khác, sửa lại cho đúng.

Kiểm tra nhanh:

```text
[ ] OrderingDb vẫn còn.
[ ] Có Services:BasketServiceBaseUrl.
[ ] BasketService đang chạy đúng port.
```

---

### 5.2. Tạo Basket DTO cho OrderingService

Tạo folder:

```text
Services/OrderingService/Application/Baskets
```

Tạo file `Application/Baskets/BasketDto.cs`:

```csharp
namespace OrderingService.Application.Baskets;

public sealed record BasketDto(
    Guid CustomerId,
    IReadOnlyList<BasketItemDto> Items);

public sealed record BasketItemDto(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity);
```

DTO này là contract nội bộ mà `OrderingService` dùng khi đọc response từ `BasketService`.

---

### 5.3. Tạo IBasketClient trong Application

Tạo file `Application/Abstractions/IBasketClient.cs`:

```csharp
using OrderingService.Application.Baskets;

namespace OrderingService.Application.Abstractions;

public interface IBasketClient
{
    Task<BasketDto?> GetBasketAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task ClearBasketAsync(Guid customerId, CancellationToken cancellationToken = default);
}
```

Handler sau này chỉ phụ thuộc `IBasketClient`, không phụ thuộc trực tiếp `HttpClient`.

---

### 5.4. Tạo HttpBasketClient trong Infrastructure

Tạo folder:

```text
Services/OrderingService/Infrastructure/Clients
```

Tạo file `Infrastructure/Clients/HttpBasketClient.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Baskets;

namespace OrderingService.Infrastructure.Clients;

public sealed class HttpBasketClient : IBasketClient
{
    private readonly HttpClient _httpClient;

    public HttpBasketClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BasketDto?> GetBasketAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/baskets/{customerId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<BasketDto>(cancellationToken);
    }

    public async Task ClearBasketAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/baskets/{customerId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }
}
```

Nếu `BasketService` endpoint của bạn không phải `/baskets/{customerId}`, sửa lại path cho đúng project hiện tại.

---

### 5.5. Tạo CheckoutCommand

Tạo folder:

```text
Services/OrderingService/Application/Orders/Checkout
```

Tạo file `Application/Orders/Checkout/CheckoutCommand.cs`:

```csharp
using MediatR;

namespace OrderingService.Application.Orders.Checkout;

public sealed record CheckoutCommand(Guid CustomerId) : IRequest<OrderDto>;
```

Ở baseline, `CustomerId` vẫn lấy từ request body để học flow. Sau này khi tích hợp auth kỹ hơn, `CustomerId` nên lấy từ JWT claim `sub/userId`.

---

### 5.6. Tạo CheckoutHandler

Tạo file `Application/Orders/Checkout/CheckoutHandler.cs`:

```csharp
using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.Checkout;

public sealed class CheckoutHandler : IRequestHandler<CheckoutCommand, OrderDto>
{
    private readonly IBasketClient _basketClient;
    private readonly IOrderRepository _orderRepository;

    public CheckoutHandler(
        IBasketClient basketClient,
        IOrderRepository orderRepository)
    {
        _basketClient = basketClient;
        _orderRepository = orderRepository;
    }

    public async Task<OrderDto> Handle(CheckoutCommand request, CancellationToken cancellationToken)
    {
        var basket = await _basketClient.GetBasketAsync(request.CustomerId, cancellationToken);

        if (basket is null || basket.Items.Count == 0)
        {
            throw new InvalidOperationException("Basket is empty.");
        }

        var order = new Order(
            Guid.NewGuid(),
            request.CustomerId,
            DateTime.UtcNow,
            OrderStatus.Pending);

        foreach (var item in basket.Items)
        {
            order.AddItem(new OrderItem(
                Guid.NewGuid(),
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity));
        }

        var createdOrder = await _orderRepository.CreateAsync(order, cancellationToken);

        await _basketClient.ClearBasketAsync(request.CustomerId, cancellationToken);

        return OrderMapper.ToDto(createdOrder);
    }
}
```

Điểm cần hiểu:

```text
BasketService trả basket.
OrderingService tạo Order snapshot từ basket.
OrderItem lưu ProductName/UnitPrice tại thời điểm checkout.
Clear basket chỉ chạy sau khi order đã tạo thành công.
```

---

### 5.7. Tạo CheckoutEndpoint

Tạo file riêng:

```text
API/Endpoints/CheckoutEndpoints.cs
```

```csharp
using MediatR;
using OrderingService.Application.Orders.Checkout;

namespace OrderingService.API.Endpoints;

public static class CheckoutEndpoints
{
    public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/checkout")
            .WithTags("Checkout");

        group.MapPost("/", async (
            CheckoutRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var command = new CheckoutCommand(request.CustomerId);
                var result = await sender.Send(command, cancellationToken);

                return Results.Created($"/orders/{result.Id}", result);
            }
            catch (InvalidOperationException ex) when (ex.Message == "Basket is empty.")
            {
                return Results.BadRequest(new
                {
                    Error = ex.Message
                });
            }
        });

        return app;
    }

    private sealed record CheckoutRequest(Guid CustomerId);
}
```

Endpoint này mỏng:

```text
HTTP request → CheckoutCommand → Handler → HTTP response
```

---

### 5.8. Đăng ký HttpBasketClient trong Program.cs

Sửa `Program.cs`.

Thêm using:

```csharp
using OrderingService.Infrastructure.Clients;
```

Đăng ký typed HttpClient:

```csharp
builder.Services.AddHttpClient<IBasketClient, HttpBasketClient>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

    var baseUrl = configuration["Services:BasketServiceBaseUrl"]
        ?? throw new InvalidOperationException("Services:BasketServiceBaseUrl is missing.");

    client.BaseAddress = new Uri(baseUrl);
});
```

Map checkout endpoint:

```csharp
app.MapOrderEndpoints();
app.MapCheckoutEndpoints();
```

---

### 5.9. Build và chạy services

Chạy các service cần thiết:

```bash
dotnet run --project Services/BasketService/BasketService.csproj
dotnet run --project Services/OrderingService/OrderingService.csproj
```

Nếu flow cần product data từ `CatalogService` khi tạo basket, chạy thêm:

```bash
dotnet run --project Services/CatalogService/CatalogService.csproj
```

Build kiểm tra:

```bash
dotnet build Services/OrderingService/OrderingService.csproj
```

Kiểm tra nhanh:

```text
[ ] BasketService chạy.
[ ] OrderingService chạy.
[ ] OrderingService đọc được BasketServiceBaseUrl.
[ ] POST /checkout xuất hiện trong Swagger/Postman.
```

---

## 6. Test bằng Postman

Tạo hoặc cập nhật environment:

| Variable | Initial value |
| --- | --- |
| `basket_url` | `http://localhost:5002` |
| `ordering_url` | `http://localhost:5004` |
| `customer_id` | `11111111-1111-1111-1111-111111111111` |
| `order_id` | để trống |

Tạo Collection:

```text
MicroShop - Lesson 16 Checkout
```

### Danh sách request

| # | Request | Method | URL | Expected |
| --- | --- | --- | --- | --- |
| 1 | Create/Update Basket | `POST/PUT` | `{{basket_url}}/baskets/{{customer_id}}` | `200/204` |
| 2 | Get Basket | `GET` | `{{basket_url}}/baskets/{{customer_id}}` | `200 OK` |
| 3 | Checkout | `POST` | `{{ordering_url}}/checkout` | `201 Created` |
| 4 | Get Order By Id | `GET` | `{{ordering_url}}/orders/{{order_id}}` | `200 OK` |
| 5 | Get Basket After Checkout | `GET` | `{{basket_url}}/baskets/{{customer_id}}` | Empty/404 tùy BasketService |
| 6 | Checkout Empty Basket | `POST` | `{{ordering_url}}/checkout` | `400 Bad Request` |

### Basket body mẫu

Tùy endpoint hiện tại của `BasketService`, body có thể cần chỉnh lại. Mẫu gợi ý:

```json
{
  "customerId": "11111111-1111-1111-1111-111111111111",
  "items": [
    {
      "productId": "22222222-2222-2222-2222-222222222222",
      "productName": "MacBook Pro",
      "unitPrice": 1999,
      "quantity": 1
    },
    {
      "productId": "33333333-3333-3333-3333-333333333333",
      "productName": "Magic Mouse",
      "unitPrice": 99,
      "quantity": 2
    }
  ]
}
```

### Checkout body

```json
{
  "customerId": "11111111-1111-1111-1111-111111111111"
}
```

Expected response:

```text
HTTP 201 Created
status = Pending
totalAmount = 2197
items có 2 dòng
```

Tab `Tests` của request `Checkout`:

```javascript
const json = pm.response.json();
pm.environment.set("order_id", json.id);
```

Kiểm tra nhanh:

```text
[ ] Basket có items trước checkout.
[ ] Checkout trả 201.
[ ] Response có order_id.
[ ] GET /orders/{{order_id}} trả đúng order.
[ ] Basket sau checkout bị clear.
[ ] Checkout lại khi basket empty trả 400.
```

---

## 7. Checklist hoàn thành

```text
[ ] OrderingService có config Services:BasketServiceBaseUrl.
[ ] Có BasketDto và BasketItemDto.
[ ] Có IBasketClient trong Application.
[ ] Có HttpBasketClient trong Infrastructure.
[ ] Program.cs đăng ký AddHttpClient<IBasketClient, HttpBasketClient>.
[ ] Có CheckoutCommand.
[ ] Có CheckoutHandler.
[ ] Có CheckoutEndpoints.
[ ] POST /checkout gọi được.
[ ] Checkout lấy basket từ BasketService.
[ ] Checkout tạo order từ basket items.
[ ] Checkout clear basket sau khi tạo order.
[ ] Postman lưu được order_id.
[ ] GET /orders/{order_id} trả đúng order.
[ ] Checkout empty basket trả 400 hoặc lỗi được xử lý rõ.
```

---

## 8. Bài tập

### Bài 1

Vẽ flow checkout:

```text
POST /checkout
→ CheckoutEndpoint
→ CheckoutCommand
→ CheckoutHandler
→ IBasketClient.GetBasketAsync
→ Order domain model
→ IOrderRepository.CreateAsync
→ IBasketClient.ClearBasketAsync
→ OrderDto
```

### Bài 2

Test bằng Postman và ghi lại kết quả:

| Request | Expected | Actual |
| --- | --- | --- |
| Get Basket trước checkout | `200` | |
| Checkout | `201` | |
| Get Order By Id | `200` | |
| Get Basket sau checkout | Empty/`404` | |
| Checkout empty basket | `400` | |

### Bài 3

Tự giải thích:

```text
Vì sao OrderingService không query trực tiếp database/Redis của BasketService?
```

### Bài 4

Design question:

```text
Nếu order tạo thành công nhưng clear basket thất bại thì hệ thống có thể gặp vấn đề gì?
```

Gợi ý:

```text
Basket vẫn còn item, user có thể checkout lại và tạo duplicate order.
Sau này cần idempotency, retry, outbox hoặc saga.
```

---

## 9. Quiz / Review

**Câu 1. Checkout trong bài này là gì?**

```text
A. API xem sản phẩm
B. Flow lấy basket và tạo order
C. Flow đăng nhập user
```

Đáp án: B

**Câu 2. OrderingService nên lấy basket bằng cách nào?**

```text
A. Query trực tiếp database của BasketService
B. Gọi API/client abstraction của BasketService
C. Copy code BasketService vào OrderingService
```

Đáp án: B

**Câu 3. Vì sao OrderItem lưu ProductName và UnitPrice?**

```text
A. Để lưu snapshot tại thời điểm checkout
B. Vì không cần ProductId
C. Vì BasketService không có ProductId
```

Đáp án: A

**Câu 4. Rủi ro của checkout baseline là gì?**

```text
A. Không thể tạo order
B. Create order thành công nhưng clear basket thất bại
C. Không thể gọi Postman
```

Đáp án: B

Review cuối bài:

```text
Bài 16 giúp MicroShop tiến gần checkout thật ở điểm nào?
```

Gợi ý:

```text
Trước bài 16, order được tạo bằng request body thủ công.
Sau bài 16, OrderingService biết lấy basket hiện tại, tạo order và clear basket sau checkout.
```

---

## 10. Chưa học trong bài này

Chưa học sâu:

```text
[ ] Lấy CustomerId từ JWT claim.
[ ] Validate product price từ CatalogService.
[ ] Payment authorization/capture.
[ ] Inventory/stock reservation.
[ ] Idempotency key.
[ ] Retry policy với Polly.
[ ] Outbox pattern.
[ ] Event OrderCreated.
[ ] Saga/process manager.
[ ] Distributed tracing.
[ ] Standard ProblemDetails error response.
```

---

## 11. Học phần nâng cao ở đâu?

| Chủ đề | Học ở đâu |
| --- | --- |
| Auth lấy CustomerId từ JWT | Khi nâng cấp Ordering security |
| Internal communication chuẩn hơn | Các bài service communication |
| Retry/timeout/circuit breaker | Stage 2 |
| Event OrderCreated | Event-driven stage |
| Outbox pattern | Stage 2 |
| Saga/process manager | Stage 2/3 |
| Payment + Inventory consistency | Các bài Payment/Inventory |

Production mindset cần nhớ:

```text
Checkout là một trong những flow nhạy cảm nhất của e-commerce.
Baseline hôm nay giúp hiểu flow, nhưng production cần idempotency, consistency, observability và error handling tốt hơn nhiều.
```

---

## Điều kiện mở khóa Buổi 17

Bạn có thể sang Buổi 17 khi:

```text
[ ] POST /checkout tạo được order từ basket.
[ ] Basket được clear sau checkout.
[ ] GET /orders/{id} xem được order vừa tạo.
[ ] Bạn hiểu vì sao OrderingService không đọc DB của BasketService.
[ ] Bạn nêu được rủi ro nếu clear basket thất bại.
```

Buổi 17:

```text
Service Communication Hardening Intro
```

Mục tiêu Buổi 17:

```text
Làm cho internal HTTP call ổn hơn: config rõ ràng, timeout, lỗi dễ hiểu, và chuẩn bị cho retry/circuit breaker sau này.
```
