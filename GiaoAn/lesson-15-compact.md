---
lesson: 15
title: "OrderingService Foundation"
duration: "90–120 phút"
phase: "Order Foundation"
project: "MicroShop"
testing: "Postman-first"
---

# Buổi 15: OrderingService Foundation

## 1. Mục tiêu

Bài này tập trung vào việc tạo `OrderingService` cơ bản cho MicroShop.

Sau bài này, cần làm được:

- Tạo service mới tên `OrderingService`.
- Thiết kế order model cơ bản.
- Tạo API tạo order và xem order.
- Lưu order bằng database riêng của `OrderingService`.
- Test được flow tạo order bằng Postman.
- Hiểu vì sao `OrderingService` không dùng chung database với `CatalogService`.

Bài này chưa làm checkout hoàn chỉnh, chưa gọi `BasketService`, chưa tích hợp payment, chưa dùng message broker.

Câu nhớ:

```text
OrderingService là service chịu trách nhiệm quản lý vòng đời đơn hàng.
Không phải nơi xử lý giỏ hàng, thanh toán hoặc tồn kho.
```

---

## 2. Bài này giải quyết vấn đề gì?

Từ các bài trước, MicroShop đã có:

```text
CatalogService  → quản lý sản phẩm
BasketService   → quản lý giỏ hàng
IdentityService → login và JWT
CatalogService  → đã có role/claim authorization
```

Nhưng hệ thống vẫn thiếu phần quan trọng nhất của e-commerce:

```text
Đơn hàng
```

Nếu chưa có `OrderingService`, hệ thống chỉ mới dừng ở mức:

```text
Xem sản phẩm
Thêm sản phẩm vào giỏ
Login user
```

Chưa có bước:

```text
Tạo order
Xem order
Theo dõi trạng thái order
Chuẩn bị cho checkout/payment
```

Buổi 15 tạo nền cho phần này.

---

## 3. Ý tưởng chính

### OrderingService chịu trách nhiệm gì?

`OrderingService` chịu trách nhiệm với các nghiệp vụ liên quan đến order:

```text
Tạo order
Xem danh sách order
Xem chi tiết order
Lưu order item
Quản lý trạng thái order
```

Trong bài này chỉ làm bản baseline:

```text
POST /orders
GET /orders
GET /orders/{id}
```

### OrderingService không chịu trách nhiệm gì?

Không nên nhét mọi thứ vào `OrderingService`.

| Việc | Service phù hợp |
| --- | --- |
| Quản lý sản phẩm | `CatalogService` |
| Quản lý giỏ hàng | `BasketService` |
| Đăng nhập/user/token | `IdentityService` |
| Tạo và quản lý order | `OrderingService` |
| Thanh toán | `PaymentService` sau này |
| Mã giảm giá | `DiscountService` sau này |
| Gửi email/thông báo | `NotificationService` sau này |

Câu nhớ:

```text
Một service tốt nên có boundary rõ.
OrderingService không phải service ôm hết mọi thứ.
```

### Database riêng cho OrderingService

Trong microservices, mỗi service nên sở hữu dữ liệu của chính nó.

Buổi này, `OrderingService` sẽ có database riêng, ví dụ:

```text
ordering.db
```

Không dùng chung database với `CatalogService`.

Lý do:

```text
CatalogService sở hữu Products.
OrderingService sở hữu Orders.
Mỗi service tự quản schema và dữ liệu của mình.
```

Sau này nếu cần thông tin sản phẩm trong order, `OrderingService` không nên join trực tiếp database của `CatalogService`.

Thay vào đó có thể dùng:

```text
- Snapshot product info khi tạo order.
- Gọi API/gRPC sang CatalogService.
- Dùng event để đồng bộ read model.
```

Bài này dùng hướng đơn giản nhất:

```text
Client gửi productName và unitPrice trong request tạo order.
OrderingService lưu snapshot vào OrderItem.
```

Sau này Buổi 16 sẽ nâng cấp checkout flow.

---

## 4. Flow tổng quan

Flow tạo order baseline:

```text
Client
→ POST /orders
→ OrderingService API
→ CreateOrderCommand
→ CreateOrderHandler
→ Order domain model
→ IOrderRepository
→ DapperOrderRepository
→ Ordering database
→ trả OrderDto
```

Flow dependency:

```text
API → Application → Domain
Infrastructure → Application + Domain
Domain không phụ thuộc database/framework
```

Bài này tiếp tục dùng style Clean Architecture baseline giống `CatalogService`.

---

## 5. Thực hành

### 5.1. Tạo OrderingService project

Đứng ở root solution `MicroShop`, tạo project:

```bash
dotnet new webapi -n OrderingService -o Services/OrderingService
dotnet sln add Services/OrderingService/OrderingService.csproj
```

Chạy thử:

```bash
dotnet run --project Services/OrderingService/OrderingService.csproj
```

Nếu template sinh sẵn `WeatherForecast`, có thể xóa để service gọn hơn.

Port gợi ý:

```text
ApiGateway       5000
CatalogService   5001
BasketService    5002
IdentityService  5003
OrderingService  5004
```

Sửa `Services/OrderingService/Properties/launchSettings.json` nếu cần:

```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5004",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

Kiểm tra nhanh:

```text
[ ] OrderingService build được.
[ ] OrderingService chạy được.
[ ] Service dùng port dễ nhớ, ví dụ 5004.
```

---

### 5.2. Cài package cần thiết

Bài này dùng Minimal API + MediatR + Dapper + SQLite.

Cài package:

```bash
dotnet add Services/OrderingService/OrderingService.csproj package MediatR
dotnet add Services/OrderingService/OrderingService.csproj package Dapper
dotnet add Services/OrderingService/OrderingService.csproj package Microsoft.Data.Sqlite
```

Build lại:

```bash
dotnet build Services/OrderingService/OrderingService.csproj
```

Kiểm tra nhanh:

```text
[ ] Có MediatR.
[ ] Có Dapper.
[ ] Có Microsoft.Data.Sqlite.
[ ] Build pass.
```

---

### 5.3. Tạo folder structure

Tạo cấu trúc giống Clean Architecture baseline:

```text
Services/OrderingService/
├── API/
│   └── Endpoints/
├── Application/
│   ├── Abstractions/
│   └── Orders/
│       ├── CreateOrder/
│       ├── GetOrderById/
│       └── GetOrders/
├── Domain/
│   └── Orders/
├── Infrastructure/
│   └── Persistence/
├── Program.cs
└── appsettings.Development.json
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force Services/OrderingService/API/Endpoints
New-Item -ItemType Directory -Force Services/OrderingService/Application/Abstractions
New-Item -ItemType Directory -Force Services/OrderingService/Application/Orders/CreateOrder
New-Item -ItemType Directory -Force Services/OrderingService/Application/Orders/GetOrderById
New-Item -ItemType Directory -Force Services/OrderingService/Application/Orders/GetOrders
New-Item -ItemType Directory -Force Services/OrderingService/Domain/Orders
New-Item -ItemType Directory -Force Services/OrderingService/Infrastructure/Persistence
```

Kiểm tra nhanh:

```text
[ ] Có API/Endpoints.
[ ] Có Application/Orders.
[ ] Có Domain/Orders.
[ ] Có Infrastructure/Persistence.
```

---

### 5.4. Thêm connection string

Sửa `Services/OrderingService/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "OrderingDb": "Data Source=ordering.db"
  }
}
```

Database này thuộc riêng `OrderingService`.

Không dùng database của `CatalogService`.

---

### 5.5. Tạo Domain model Order và OrderItem

Tạo file `Domain/Orders/OrderStatus.cs`:

```csharp
namespace OrderingService.Domain.Orders;

public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Cancelled = 3,
    Completed = 4
}
```

Tạo file `Domain/Orders/OrderItem.cs`:

```csharp
namespace OrderingService.Domain.Orders;

public sealed class OrderItem
{
    public Guid Id { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public decimal UnitPrice { get; }
    public int Quantity { get; }

    public decimal TotalPrice => UnitPrice * Quantity;

    public OrderItem(Guid id, Guid productId, string productName, decimal unitPrice, int quantity)
    {
        if (id == Guid.Empty) throw new ArgumentException("Order item id cannot be empty.", nameof(id));
        if (productId == Guid.Empty) throw new ArgumentException("Product id cannot be empty.", nameof(productId));
        if (string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("Product name is required.", nameof(productName));
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        Id = id;
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }
}
```

Tạo file `Domain/Orders/Order.cs`:

```csharp
namespace OrderingService.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    public Guid Id { get; }
    public Guid CustomerId { get; }
    public DateTime CreatedAtUtc { get; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items;
    public decimal TotalAmount => _items.Sum(item => item.TotalPrice);

    public Order(Guid id, Guid customerId, DateTime createdAtUtc, OrderStatus status)
    {
        if (id == Guid.Empty) throw new ArgumentException("Order id cannot be empty.", nameof(id));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer id cannot be empty.", nameof(customerId));

        Id = id;
        CustomerId = customerId;
        CreatedAtUtc = createdAtUtc;
        Status = status;
    }

    public void AddItem(OrderItem item)
    {
        _items.Add(item);
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException("Only pending orders can be confirmed.");
        }

        Status = OrderStatus.Confirmed;
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Completed)
        {
            throw new InvalidOperationException("Completed orders cannot be cancelled.");
        }

        Status = OrderStatus.Cancelled;
    }
}
```

Điểm cần hiểu:

```text
Order giữ danh sách item.
OrderItem lưu snapshot ProductName/UnitPrice.
TotalAmount được tính từ Items.
Domain tự bảo vệ rule cơ bản.
```

---

### 5.6. Tạo DTO và repository interface

Tạo file `Application/Orders/OrderDto.cs`:

```csharp
namespace OrderingService.Application.Orders;

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    DateTime CreatedAtUtc,
    string Status,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice);
```

Tạo file `Application/Orders/OrderMapper.cs`:

```csharp
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders;

public static class OrderMapper
{
    public static OrderDto ToDto(Order order)
    {
        return new OrderDto(
            order.Id,
            order.CustomerId,
            order.CreatedAtUtc,
            order.Status.ToString(),
            order.TotalAmount,
            order.Items.Select(item => new OrderItemDto(
                item.Id,
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity,
                item.TotalPrice)).ToList());
    }
}
```

Tạo file `Application/Abstractions/IOrderRepository.cs`:

```csharp
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Abstractions;

public interface IOrderRepository
{
    Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default);
}
```

Repository interface nằm trong `Application`, implementation nằm trong `Infrastructure`.

---

### 5.7. Tạo persistence cho OrderingService

Tạo file `Infrastructure/Persistence/IDbConnectionFactory.cs`:

```csharp
using System.Data;

namespace OrderingService.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
```

Tạo file `Infrastructure/Persistence/SqliteConnectionFactory.cs`:

```csharp
using System.Data;
using Microsoft.Data.Sqlite;

namespace OrderingService.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OrderingDb")
            ?? throw new InvalidOperationException("Connection string 'OrderingDb' is missing.");
    }

    public IDbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}
```

Tạo file `Infrastructure/Persistence/DatabaseInitializer.cs`:

```csharp
using Dapper;

namespace OrderingService.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        using var connection = connectionFactory.CreateConnection();

        const string sql = """
            CREATE TABLE IF NOT EXISTS Orders (
                Id TEXT PRIMARY KEY,
                CustomerId TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                Status TEXT NOT NULL,
                TotalAmount REAL NOT NULL
            );

            CREATE TABLE IF NOT EXISTS OrderItems (
                Id TEXT PRIMARY KEY,
                OrderId TEXT NOT NULL,
                ProductId TEXT NOT NULL,
                ProductName TEXT NOT NULL,
                UnitPrice REAL NOT NULL,
                Quantity INTEGER NOT NULL,
                TotalPrice REAL NOT NULL,
                FOREIGN KEY (OrderId) REFERENCES Orders(Id)
            );
            """;

        await connection.ExecuteAsync(sql);
    }
}
```

Bài này dùng SQLite local để học nhanh. Production sẽ dùng PostgreSQL/SQL Server và migration chuẩn hơn.

---

### 5.8. Tạo DapperOrderRepository

Tạo file `Infrastructure/Persistence/DapperOrderRepository.cs`:

```csharp
using Dapper;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Orders;

namespace OrderingService.Infrastructure.Persistence;

public sealed class DapperOrderRepository : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperOrderRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string ordersSql = """
            SELECT Id, CustomerId, CreatedAtUtc, Status
            FROM Orders
            ORDER BY CreatedAtUtc DESC
            """;

        const string itemsSql = """
            SELECT Id, OrderId, ProductId, ProductName, UnitPrice, Quantity
            FROM OrderItems
            """;

        using var connection = _connectionFactory.CreateConnection();

        var orderRows = (await connection.QueryAsync<OrderRow>(ordersSql)).ToList();
        var itemRows = (await connection.QueryAsync<OrderItemRow>(itemsSql)).ToList();

        return MapOrders(orderRows, itemRows);
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string orderSql = """
            SELECT Id, CustomerId, CreatedAtUtc, Status
            FROM Orders
            WHERE Id = @Id
            """;

        const string itemsSql = """
            SELECT Id, OrderId, ProductId, ProductName, UnitPrice, Quantity
            FROM OrderItems
            WHERE OrderId = @OrderId
            """;

        using var connection = _connectionFactory.CreateConnection();

        var orderRow = await connection.QuerySingleOrDefaultAsync<OrderRow>(orderSql, new { Id = id.ToString() });

        if (orderRow is null)
        {
            return null;
        }

        var itemRows = await connection.QueryAsync<OrderItemRow>(itemsSql, new { OrderId = id.ToString() });

        return MapOrder(orderRow, itemRows);
    }

    public async Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default)
    {
        const string insertOrderSql = """
            INSERT INTO Orders (Id, CustomerId, CreatedAtUtc, Status, TotalAmount)
            VALUES (@Id, @CustomerId, @CreatedAtUtc, @Status, @TotalAmount)
            """;

        const string insertItemSql = """
            INSERT INTO OrderItems (Id, OrderId, ProductId, ProductName, UnitPrice, Quantity, TotalPrice)
            VALUES (@Id, @OrderId, @ProductId, @ProductName, @UnitPrice, @Quantity, @TotalPrice)
            """;

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(insertOrderSql, new
        {
            Id = order.Id.ToString(),
            CustomerId = order.CustomerId.ToString(),
            CreatedAtUtc = order.CreatedAtUtc.ToString("O"),
            Status = order.Status.ToString(),
            order.TotalAmount
        }, transaction);

        foreach (var item in order.Items)
        {
            await connection.ExecuteAsync(insertItemSql, new
            {
                Id = item.Id.ToString(),
                OrderId = order.Id.ToString(),
                ProductId = item.ProductId.ToString(),
                item.ProductName,
                item.UnitPrice,
                item.Quantity,
                item.TotalPrice
            }, transaction);
        }

        transaction.Commit();

        return order;
    }

    private static IReadOnlyList<Order> MapOrders(
        IReadOnlyList<OrderRow> orderRows,
        IReadOnlyList<OrderItemRow> itemRows)
    {
        return orderRows
            .Select(orderRow =>
            {
                var orderItems = itemRows.Where(item => item.OrderId == orderRow.Id);
                return MapOrder(orderRow, orderItems);
            })
            .ToList();
    }

    private static Order MapOrder(OrderRow row, IEnumerable<OrderItemRow> itemRows)
    {
        var order = new Order(
            Guid.Parse(row.Id),
            Guid.Parse(row.CustomerId),
            DateTime.Parse(row.CreatedAtUtc),
            Enum.Parse<OrderStatus>(row.Status));

        foreach (var itemRow in itemRows)
        {
            order.AddItem(new OrderItem(
                Guid.Parse(itemRow.Id),
                Guid.Parse(itemRow.ProductId),
                itemRow.ProductName,
                itemRow.UnitPrice,
                itemRow.Quantity));
        }

        return order;
    }

    private sealed record OrderRow(
        string Id,
        string CustomerId,
        string CreatedAtUtc,
        string Status);

    private sealed record OrderItemRow(
        string Id,
        string OrderId,
        string ProductId,
        string ProductName,
        decimal UnitPrice,
        int Quantity);
}
```

Điểm cần hiểu:

```text
DapperOrderRepository làm việc với DB.
Handler không biết SQL.
Domain không biết SQLite.
Order và OrderItem vẫn là domain model.
```

---

### 5.9. Tạo CreateOrder use case

Tạo file `Application/Orders/CreateOrder/CreateOrderCommand.cs`:

```csharp
using MediatR;

namespace OrderingService.Application.Orders.CreateOrder;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    IReadOnlyList<CreateOrderItemCommand> Items) : IRequest<OrderDto>;

public sealed record CreateOrderItemCommand(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity);
```

Tạo file `Application/Orders/CreateOrder/CreateOrderHandler.cs`:

```csharp
using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.CreateOrder;

public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderDto>
{
    private readonly IOrderRepository _repository;

    public CreateOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Order must have at least one item.");
        }

        var order = new Order(
            Guid.NewGuid(),
            request.CustomerId,
            DateTime.UtcNow,
            OrderStatus.Pending);

        foreach (var item in request.Items)
        {
            order.AddItem(new OrderItem(
                Guid.NewGuid(),
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity));
        }

        var createdOrder = await _repository.CreateAsync(order, cancellationToken);

        return OrderMapper.ToDto(createdOrder);
    }
}
```

Bài này chưa thêm FluentValidation để tránh nhồi quá nhiều. Domain vẫn tự bảo vệ rule cơ bản. Validation nâng cao có thể thêm sau.

---

### 5.10. Tạo GetOrders và GetOrderById use case

Tạo `Application/Orders/GetOrders/GetOrdersQuery.cs`:

```csharp
using MediatR;

namespace OrderingService.Application.Orders.GetOrders;

public sealed record GetOrdersQuery : IRequest<IReadOnlyList<OrderDto>>;
```

Tạo `Application/Orders/GetOrders/GetOrdersHandler.cs`:

```csharp
using MediatR;
using OrderingService.Application.Abstractions;

namespace OrderingService.Application.Orders.GetOrders;

public sealed class GetOrdersHandler : IRequestHandler<GetOrdersQuery, IReadOnlyList<OrderDto>>
{
    private readonly IOrderRepository _repository;

    public GetOrdersHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<OrderDto>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _repository.GetAllAsync(cancellationToken);

        return orders
            .Select(OrderMapper.ToDto)
            .ToList();
    }
}
```

Tạo `Application/Orders/GetOrderById/GetOrderByIdQuery.cs`:

```csharp
using MediatR;

namespace OrderingService.Application.Orders.GetOrderById;

public sealed record GetOrderByIdQuery(Guid Id) : IRequest<OrderDto?>;
```

Tạo `Application/Orders/GetOrderById/GetOrderByIdHandler.cs`:

```csharp
using MediatR;
using OrderingService.Application.Abstractions;

namespace OrderingService.Application.Orders.GetOrderById;

public sealed class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, OrderDto?>
{
    private readonly IOrderRepository _repository;

    public GetOrderByIdHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrderDto?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(request.Id, cancellationToken);

        return order is null
            ? null
            : OrderMapper.ToDto(order);
    }
}
```

---

### 5.11. Tạo OrderEndpoints

Tạo file `API/Endpoints/OrderEndpoints.cs`:

```csharp
using MediatR;
using OrderingService.Application.Orders.CreateOrder;
using OrderingService.Application.Orders.GetOrderById;
using OrderingService.Application.Orders.GetOrders;

namespace OrderingService.API.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders")
            .WithTags("Orders");

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetOrdersQuery(), cancellationToken);
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetOrderByIdQuery(id), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/", async (CreateOrderRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new CreateOrderCommand(
                request.CustomerId,
                request.Items.Select(item => new CreateOrderItemCommand(
                    item.ProductId,
                    item.ProductName,
                    item.UnitPrice,
                    item.Quantity)).ToList());

            var result = await sender.Send(command, cancellationToken);

            return Results.Created($"/orders/{result.Id}", result);
        });

        return app;
    }

    private sealed record CreateOrderRequest(
        Guid CustomerId,
        IReadOnlyList<CreateOrderItemRequest> Items);

    private sealed record CreateOrderItemRequest(
        Guid ProductId,
        string ProductName,
        decimal UnitPrice,
        int Quantity);
}
```

Endpoint chỉ nhận request, map sang command, gọi handler qua MediatR.

---

### 5.12. Cấu hình Program.cs

Sửa `Program.cs`:

```csharp
using OrderingService.API.Endpoints;
using OrderingService.Application.Abstractions;
using OrderingService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
builder.Services.AddScoped<IOrderRepository, DapperOrderRepository>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapOrderEndpoints();

app.Run();
```

Kiểm tra nhanh:

```text
[ ] Program.cs không chứa SQL.
[ ] Program.cs register IOrderRepository.
[ ] Program.cs gọi DatabaseInitializer.
[ ] Program.cs map OrderEndpoints.
```

---

## 6. Test bằng Postman

Tạo Environment hoặc dùng environment cũ:

| Variable | Initial value |
| --- | --- |
| `ordering_url` | `http://localhost:5004` |
| `order_id` | để trống |

Tạo Collection:

```text
MicroShop - Lesson 15 Ordering
```

### Danh sách request

| # | Request | Method | URL | Expected |
| --- | --- | --- | --- | --- |
| 1 | Create Order | `POST` | `{{ordering_url}}/orders` | `201 Created` |
| 2 | Get Orders | `GET` | `{{ordering_url}}/orders` | `200 OK` |
| 3 | Get Order By Id | `GET` | `{{ordering_url}}/orders/{{order_id}}` | `200 OK` |
| 4 | Get Order Not Found | `GET` | `{{ordering_url}}/orders/00000000-0000-0000-0000-000000000000` | `404 Not Found` |

### Create Order body

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

Expected response:

```text
HTTP 201 Created
Có id
Có customerId
Có status = Pending
Có totalAmount = 2197
Có items
```

Trong tab `Tests` của request `Create Order`:

```javascript
const json = pm.response.json();
pm.environment.set("order_id", json.id);
```

Kiểm tra nhanh:

```text
[ ] Create Order trả 201.
[ ] Postman tự lưu order_id.
[ ] Get Orders trả danh sách order.
[ ] Get Order By Id trả đúng order vừa tạo.
[ ] Get Order Not Found trả 404.
```

---

## 7. Checklist hoàn thành

```text
[ ] Tạo được OrderingService.
[ ] OrderingService chạy port 5004.
[ ] Có folder API/Application/Domain/Infrastructure.
[ ] Có Order domain model.
[ ] Có OrderItem domain model.
[ ] Có OrderStatus enum.
[ ] Có OrderDto và OrderItemDto.
[ ] Có OrderMapper.
[ ] Có IOrderRepository.
[ ] Có SQLite connection factory.
[ ] Có DatabaseInitializer tạo bảng Orders/OrderItems.
[ ] Có DapperOrderRepository.
[ ] Có CreateOrderCommand/Handler.
[ ] Có GetOrdersQuery/Handler.
[ ] Có GetOrderByIdQuery/Handler.
[ ] Có OrderEndpoints.
[ ] Program.cs register đầy đủ dependency.
[ ] POST /orders tạo order thành công.
[ ] GET /orders trả danh sách.
[ ] GET /orders/{id} trả chi tiết order.
[ ] Postman lưu được order_id.
```

---

## 8. Bài tập

### Bài 1

Vẽ flow tạo order:

```text
POST /orders
→ OrderEndpoints
→ CreateOrderCommand
→ CreateOrderHandler
→ Order domain model
→ IOrderRepository
→ DapperOrderRepository
→ Orders/OrderItems tables
```

### Bài 2

Test bằng Postman và ghi lại kết quả:

| Request | Expected | Actual |
| --- | --- | --- |
| `POST /orders` | `201` | |
| `GET /orders` | `200` | |
| `GET /orders/{id}` | `200` | |
| `GET /orders/{empty-guid}` | `404` | |

### Bài 3

Tự giải thích:

```text
Vì sao OrderItem lưu ProductName và UnitPrice thay vì chỉ lưu ProductId?
```

Gợi ý:

```text
Order cần snapshot tại thời điểm mua.
Nếu sản phẩm đổi tên hoặc đổi giá sau này, order cũ vẫn phải giữ đúng dữ liệu lịch sử.
```

### Bài 4

Design question:

```text
OrderingService có nên query thẳng database của CatalogService để lấy product price không?
```

Gợi ý:

```text
Không nên.
Mỗi service sở hữu database riêng.
Sau này có thể gọi API/gRPC hoặc dùng event/read model.
```

---

## 9. Quiz / Review

**Câu 1. OrderingService chịu trách nhiệm chính việc gì?**

```text
A. Quản lý sản phẩm
B. Quản lý đơn hàng
C. Đăng nhập user
```

Đáp án: B

**Câu 2. Vì sao OrderItem nên lưu ProductName và UnitPrice?**

```text
A. Để lưu snapshot tại thời điểm tạo order
B. Để tránh phải tạo bảng OrderItems
C. Vì ProductId không dùng được
```

Đáp án: A

**Câu 3. Service nào sở hữu dữ liệu Orders?**

```text
A. CatalogService
B. BasketService
C. OrderingService
```

Đáp án: C

**Câu 4. Handler nên phụ thuộc gì?**

```text
A. DapperOrderRepository
B. IOrderRepository
C. SqliteConnection
```

Đáp án: B

Review cuối bài:

```text
Bài 15 giúp MicroShop tiến gần checkout flow hơn ở điểm nào?
```

Gợi ý:

```text
Trước bài 15, hệ thống chưa có khái niệm order.
Sau bài 15, MicroShop có OrderingService để tạo và đọc order, chuẩn bị cho checkout flow ở bài 16.
```

---

## 10. Chưa học trong bài này

Chưa học sâu:

```text
[ ] Checkout từ Basket sang Order.
[ ] Validate product tồn tại bằng CatalogService.
[ ] Auth: lấy CustomerId từ JWT thay vì request body.
[ ] Payment flow.
[ ] Order status transition đầy đủ.
[ ] Transaction nâng cao.
[ ] Outbox pattern.
[ ] Event-driven order created.
[ ] Saga/process manager.
[ ] Idempotency khi tạo order.
[ ] Stock reservation.
```

Lý do:

```text
Buổi 15 chỉ tạo nền OrderingService.
Nếu nhồi checkout/payment/event/outbox vào đây sẽ quá tải.
```

---

## 11. Học phần nâng cao ở đâu?

Theo roadmap gần nhất:

| Chủ đề | Học ở đâu |
| --- | --- |
| Checkout flow từ Basket sang Order | Buổi 16 |
| Gọi service khác / internal communication | Các bài sau Ordering |
| Event-driven integration | Stage 2 |
| Outbox pattern | Stage 2 |
| Payment flow | Sau Ordering baseline |
| Saga / distributed transaction mindset | Stage 2/3 |
| Idempotency | Khi học checkout/payment production |

Production mindset cần nhớ:

```text
Tạo order thật trong hệ enterprise phức tạp hơn nhiều:
phải tính idempotency, payment, stock, event, retry, audit và consistency.
Buổi 15 chỉ tạo khung order service trước.
```

---

## Điều kiện mở khóa Buổi 16

Bạn có thể sang Buổi 16 khi:

```text
[ ] OrderingService chạy được.
[ ] POST /orders tạo order thành công.
[ ] GET /orders lấy được danh sách order.
[ ] GET /orders/{id} lấy được chi tiết order.
[ ] Bạn hiểu vì sao OrderingService có database riêng.
[ ] Bạn hiểu vì sao OrderItem lưu product snapshot.
```

Buổi 16:

```text
Checkout Flow: Basket → Order
```

Mục tiêu Buổi 16:

```text
Tạo flow checkout cơ bản: lấy basket, tạo order, clear basket, chuẩn bị cho event/payment sau này.
```
