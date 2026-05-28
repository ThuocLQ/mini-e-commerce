# Buổi 26: MongoDB Read Model - OrderSummary Projection Foundation

## 0. Vị trí trong lộ trình

Bạn đã hoàn thành:

```text
Buổi 24: RabbitMQ vs Kafka - Decision Matrix
Buổi 25: Kafka Intro - Topic / Partition / Offset / Consumer Group Demo
```

Mạch Phase 1.6 hiện tại:

```text
Buổi 24: RabbitMQ vs Kafka
Buổi 25: Kafka Intro
Buổi 26: MongoDB Read Model
Buổi 27: Kafka → MongoDB Projection
```

Buổi 25 đã giúp bạn hiểu Kafka primitives:

```text
Topic
Partition
Offset
Consumer Group
Lag
Key
```

Buổi 26 sẽ chuẩn bị phần read model bằng MongoDB.

Bài này **chưa consume Kafka**.

Bài này tập trung vào:

```text
[ ] Vì sao cần read model.
[ ] Vì sao MongoDB phù hợp cho projection/read model.
[ ] Thiết kế OrderSummaryReadModel.
[ ] Chạy MongoDB local bằng Docker Compose.
[ ] Tạo endpoint đọc order summary từ MongoDB.
[ ] Tạo endpoint debug seed/upsert read model để test bằng Postman.
```

Buổi 27 mới làm flow:

```text
Kafka order-events
→ ProjectionWorker consume
→ Upsert MongoDB OrderSummaryReadModel
```

Câu nhớ:

```text
Buổi 26 dựng nơi chứa read model.
Buổi 27 mới bơm event từ Kafka vào read model.
```

---

## 1. Mục tiêu bài học

Trong 90–120 phút, mục tiêu là:

```text
[ ] Hiểu read model là gì.
[ ] Hiểu projection là gì.
[ ] Hiểu vì sao read model khác write model.
[ ] Hiểu vì sao MongoDB phù hợp với document read model.
[ ] Thêm MongoDB vào docker-compose.
[ ] Tạo service/project đọc OrderSummary từ MongoDB.
[ ] Tạo collection `order_summaries`.
[ ] Tạo MongoDB repository.
[ ] Tạo endpoint GET order summaries.
[ ] Tạo endpoint debug seed/upsert read model bằng Postman.
[ ] Chuẩn bị cấu trúc cho ProjectionWorker bài 27.
```

Output chính:

```text
Services/OrderingService hoặc Services/OrderQueryService đọc MongoDB read model
MongoDB container trong docker-compose
Collection: order_summaries
docs/mongodb-read-model-notes.md
```

Bài này có hai lựa chọn triển khai.

Khuyến nghị cho MicroShop ở stage hiện tại:

```text
Option A - đơn giản:
    Thêm read endpoint/debug endpoint vào OrderingService.

Option B - tách service rõ hơn:
    Tạo OrderQueryService chuyên đọc MongoDB.
```

Trong bài này dùng **Option A** để học nhanh trong 90–120 phút.

Lưu ý quan trọng:

```text
Option A là lựa chọn học nhanh, không phải kiến trúc cuối cùng.
OrderingService tạm đọc MongoDB read model để bạn hiểu read model trước.
Sau này có thể tách OrderQueryService và ProjectionWorker để boundary sạch hơn.
```

Sau này nếu muốn production-minded hơn có thể refactor thành:

```text
Services/OrderQueryService
Workers/ProjectionWorker
```

---

## 2. Read Model là gì?

Trong hệ thống backend, data thường có hai nhu cầu khác nhau:

```text
Write side:
    Xử lý nghiệp vụ, validate rule, lưu transaction.

Read side:
    Trả dữ liệu tối ưu cho màn hình/API query.
```

Ví dụ write model của OrderingService có thể lưu:

```text
Orders
OrderItems
OrderStatusHistory
PaymentRecords
```

Nhưng màn hình danh sách đơn hàng chỉ cần:

```text
OrderId
CustomerId
CustomerName
TotalAmount
Currency
Status
ItemCount
CreatedAtUtc
LastUpdatedAtUtc
```

Nếu cứ query nhiều bảng nghiệp vụ để dựng màn hình, hệ thống có thể:

```text
Join phức tạp.
Query chậm.
Coupling read API vào schema write DB.
Khó tối ưu cho nhiều màn hình khác nhau.
```

Read model là model dữ liệu được thiết kế riêng cho nhu cầu đọc.

Câu nhớ:

```text
Write model tối ưu cho nghiệp vụ và consistency.
Read model tối ưu cho query và UI/API response.
```

---

## 3. Projection là gì?

Projection là quá trình biến event/business data thành read model.

Ví dụ sau này:

```text
OrderCreated event
→ tạo OrderSummaryReadModel

OrderPaid event
→ update Status = Paid

OrderCancelled event
→ update Status = Cancelled
```

Flow tương lai:

```text
Kafka topic: microshop.order-events
    |
    v
ProjectionWorker
    |
    v
MongoDB collection: order_summaries
```

Projection không phải source of truth chính.

Source of truth vẫn là write database của OrderingService.

MongoDB read model có thể rebuild được từ event stream nếu cần.

Câu nhớ:

```text
Projection là bản đọc được build từ event hoặc write-side data.
```

---

## 4. Vì sao dùng MongoDB cho read model?

MongoDB là document database.

Nó phù hợp khi read response có dạng document.

Ví dụ màn hình order summary có thể lưu một document như:

```json
{
  "orderId": "ORD-001",
  "customerId": "CUST-001",
  "customerName": "Lion",
  "status": "Created",
  "totalAmount": 1977.3,
  "currency": "VND",
  "itemCount": 2,
  "items": [
    {
      "productId": "P001",
      "productName": "MacBook Pro",
      "quantity": 1,
      "unitPrice": 1977.3
    }
  ],
  "createdAtUtc": "2026-05-28T10:00:00Z",
  "lastUpdatedAtUtc": "2026-05-28T10:00:00Z"
}
```

Ưu điểm:

```text
Read API có thể trả document gần giống UI cần.
Ít join.
Dễ denormalize.
Dễ tạo nhiều collection cho nhiều read use case.
Phù hợp projection từ event stream.
```

Nhược điểm:

```text
Có eventual consistency.
Có thể duplicate data.
Cần projection logic.
Cần rebuild strategy.
Không thay thế write DB nghiệp vụ.
```

Câu nhớ:

```text
MongoDB ở đây dùng làm read model, không thay database nghiệp vụ của OrderingService.
```

---

## 5. Read model trong MicroShop

Bài này tạo read model:

```text
OrderSummaryReadModel
```

Mục tiêu phục vụ API đọc danh sách/order detail đơn giản:

```text
GET /order-summaries
GET /order-summaries/{orderId}
```

Document mẫu:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "orderId": "11111111-1111-1111-1111-111111111111",
  "customerId": "22222222-2222-2222-2222-222222222222",
  "customerName": "Demo Customer",
  "status": "Created",
  "totalAmount": 1977.3,
  "currency": "VND",
  "itemCount": 1,
  "createdAtUtc": "2026-05-28T10:00:00Z",
  "lastUpdatedAtUtc": "2026-05-28T10:00:00Z"
}
```

Trong bài 26, dữ liệu được seed/upsert bằng debug endpoint.

Trong bài 27, dữ liệu sẽ được upsert từ Kafka consumer.

---

## 6. Scope guard của bài 26

Bài này làm:

```text
[ ] Chạy MongoDB local.
[ ] Thiết kế OrderSummaryReadModel.
[ ] Tạo MongoDB repository.
[ ] Tạo API đọc read model.
[ ] Tạo debug endpoint seed/upsert để test Postman.
[ ] Ghi docs note.
```

Không làm:

```text
[ ] Không consume Kafka.
[ ] Không tạo ProjectionWorker thật.
[ ] Không publish event từ OrderingService sang Kafka.
[ ] Không làm event replay.
[ ] Không làm Change Streams.
[ ] Không làm MongoDB sharding/replica set.
[ ] Không thay thế OrderingService write DB bằng MongoDB.
```

Lý do:

```text
Buổi 26 chuẩn bị read model.
Buổi 27 mới nối Kafka → MongoDB Projection.
```

---

## 7. Thêm MongoDB vào docker-compose

Mở:

```text
docker-compose.yml
```

Thêm service:

```yaml
services:
  mongodb:
    image: mongo:7
    container_name: microshop-mongodb
    ports:
      - "27017:27017"
    environment:
      MONGO_INITDB_ROOT_USERNAME: microshop
      MONGO_INITDB_ROOT_PASSWORD: microshop
    volumes:
      - microshop_mongodb_data:/data/db

volumes:
  microshop_mongodb_data:
```

Nếu file đã có `volumes`, chỉ merge thêm:

```yaml
volumes:
  microshop_mongodb_data:
```

Chạy:

```bash
docker compose up -d mongodb
```

Kiểm tra:

```bash
docker ps
```

Expected:

```text
microshop-mongodb
```

Xem logs:

```bash
docker logs microshop-mongodb --tail 50
```

Connection string local:

```text
mongodb://microshop:microshop@localhost:27017/?authSource=admin
```

Database:

```text
MicroShop_OrderReadDb
```

Collection:

```text
order_summaries
```

---

## 8. Cài MongoDB driver cho OrderingService

Nếu chọn Option A, thêm package vào OrderingService:

```bash
dotnet add Services/OrderingService/OrderingService.csproj package MongoDB.Driver
```

Build thử:

```bash
dotnet build Services/OrderingService/OrderingService.csproj
```

Nếu muốn tách service riêng sau này, package này sẽ nằm ở OrderQueryService hoặc ProjectionWorker.

---

## 9. Thêm MongoDB config

Mở:

```text
Services/OrderingService/appsettings.Development.json
```

Thêm:

```json
{
  "MongoDb": {
    "ConnectionString": "mongodb://microshop:microshop@localhost:27017/?authSource=admin",
    "DatabaseName": "MicroShop_OrderReadDb",
    "OrderSummariesCollectionName": "order_summaries"
  }
}
```

Nếu file đã có các section khác như RabbitMQ/OutboxPublisher, merge thêm `MongoDb`.

Ví dụ:

```json
{
  "RabbitMq": {
    "Host": "localhost",
    "VirtualHost": "/",
    "UserName": "microshop",
    "Password": "microshop"
  },
  "OutboxPublisher": {
    "Enabled": true,
    "BatchSize": 10,
    "IntervalSeconds": 5,
    "MaxRetryCount": 10,
    "SimulatePublishFailure": false
  },
  "MongoDb": {
    "ConnectionString": "mongodb://microshop:microshop@localhost:27017/?authSource=admin",
    "DatabaseName": "MicroShop_OrderReadDb",
    "OrderSummariesCollectionName": "order_summaries"
  }
}
```

---

## 10. Tạo MongoDbOptions

Tạo folder:

```text
Services/OrderingService/Infrastructure/ReadModels/MongoDb
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force Services/OrderingService/Infrastructure/ReadModels/MongoDb
```

Tạo file:

```text
Services/OrderingService/Infrastructure/ReadModels/MongoDb/MongoDbOptions.cs
```

```csharp
namespace OrderingService.Infrastructure.ReadModels.MongoDb;

public sealed class MongoDbOptions
{
    public string ConnectionString { get; init; } = default!;
    public string DatabaseName { get; init; } = default!;
    public string OrderSummariesCollectionName { get; init; } = default!;
}
```

---

## 11. Tạo OrderSummaryReadModel

Tạo folder:

```text
Services/OrderingService/Application/ReadModels
```

Tạo file:

```text
Services/OrderingService/Application/ReadModels/OrderSummaryReadModel.cs
```

```csharp
namespace OrderingService.Application.ReadModels;

public sealed class OrderSummaryReadModel
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = default!;
    public string Status { get; init; } = default!;
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = default!;
    public int ItemCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime LastUpdatedAtUtc { get; init; }
}
```

Vì sao không đặt MongoDB attributes trong class này?

```text
OrderSummaryReadModel đang nằm trong Application layer.
Nếu đặt [BsonId], [BsonRepresentation] ở đây thì Application sẽ phụ thuộc MongoDB driver.
Ở stage học này vẫn có thể chấp nhận, nhưng Clean hơn là để MongoDB mapping nằm ở Infrastructure.
```

Bản bài này chọn cách đơn giản nhưng sạch hơn:

```text
Application định nghĩa shape của read model.
Infrastructure MongoDB xử lý cách lưu document.
```

Vì sao `Id` dùng `OrderId`?

```text
Read model mỗi order có một document summary.
Dùng OrderId làm Id giúp upsert dễ hơn.
```

Bài này dùng:

```text
Id = OrderId
```

---

## 12. Tạo repository abstraction

Tạo file:

```text
Services/OrderingService/Application/Abstractions/IOrderSummaryReadRepository.cs
```

```csharp
using OrderingService.Application.ReadModels;

namespace OrderingService.Application.Abstractions;

public interface IOrderSummaryReadRepository
{
    Task UpsertAsync(
        OrderSummaryReadModel model,
        CancellationToken cancellationToken = default);

    Task<OrderSummaryReadModel?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderSummaryReadModel>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
```

---

## 13. Tạo MongoDB repository

Tạo file:

```text
Services/OrderingService/Infrastructure/ReadModels/MongoDb/MongoOrderSummaryReadRepository.cs
```

```csharp
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OrderingService.Application.Abstractions;
using OrderingService.Application.ReadModels;

namespace OrderingService.Infrastructure.ReadModels.MongoDb;

public sealed class MongoOrderSummaryReadRepository : IOrderSummaryReadRepository
{
    private readonly IMongoCollection<OrderSummaryReadModel> _collection;

    public MongoOrderSummaryReadRepository(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options)
    {
        var mongoOptions = options.Value;
        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);

        _collection = database.GetCollection<OrderSummaryReadModel>(
            mongoOptions.OrderSummariesCollectionName);

        EnsureIndexes();
    }

    public async Task UpsertAsync(
        OrderSummaryReadModel model,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OrderSummaryReadModel>.Filter.Eq(x => x.Id, model.Id);

        await _collection.ReplaceOneAsync(
            filter,
            model,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<OrderSummaryReadModel?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OrderSummaryReadModel>.Filter.Eq(x => x.OrderId, orderId);

        return await _collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderSummaryReadModel>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(Builders<OrderSummaryReadModel>.Filter.Empty)
            .SortByDescending(x => x.CreatedAtUtc)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    private void EnsureIndexes()
    {
        var orderIdIndex = new CreateIndexModel<OrderSummaryReadModel>(
            Builders<OrderSummaryReadModel>.IndexKeys.Ascending(x => x.OrderId),
            new CreateIndexOptions
            {
                Name = "UX_order_summaries_orderId",
                Unique = true
            });

        var latestIndex = new CreateIndexModel<OrderSummaryReadModel>(
            Builders<OrderSummaryReadModel>.IndexKeys.Descending(x => x.CreatedAtUtc),
            new CreateIndexOptions
            {
                Name = "IX_order_summaries_createdAtUtc_desc"
            });

        _collection.Indexes.CreateMany(new[]
        {
            orderIdIndex,
            latestIndex
        });
    }
}
```

Điểm đã chỉnh cho bản chuẩn hơn:

```text
[ ] IMongoClient được inject singleton, không tạo MongoClient mới trong repository.
[ ] Upsert filter theo Id vì bài này quy ước Id = OrderId.
[ ] Vẫn có unique index theo OrderId để chống duplicate do lỗi mapping.
[ ] Có index CreatedAtUtc descending cho API latest.
[ ] Index được tạo bằng code để không phụ thuộc thao tác mongosh thủ công.
```

Lưu ý:

```text
EnsureIndexes chạy khi repository được tạo.
Bài học local dùng cách này cho đơn giản.
Production có thể tách index migration/init riêng.
```

---

## 14. Register MongoDB repository

Mở:

```text
Services/OrderingService/Program.cs
```

Thêm using:

```csharp
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OrderingService.Application.Abstractions;
using OrderingService.Infrastructure.ReadModels.MongoDb;
```

Thêm DI:

```csharp
builder.Services.Configure<MongoDbOptions>(
    builder.Configuration.GetSection("MongoDb"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
    return new MongoClient(options.ConnectionString);
});

builder.Services.AddSingleton<IOrderSummaryReadRepository, MongoOrderSummaryReadRepository>();
```

Vì sao `IMongoClient` là singleton?

```text
MongoClient được thiết kế để reuse.
Không nên tạo mới MongoClient cho mỗi request/repository call.
```

Build:

```bash
dotnet build Services/OrderingService/OrderingService.csproj
```

---

## 15. Tạo request debug seed read model

Tạo DTO nếu bạn muốn gọn hơn.

Ví dụ trong `Program.cs` hoặc folder endpoint:

```csharp
public sealed record DebugUpsertOrderSummaryRequest(
    Guid OrderId,
    Guid CustomerId,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    string Currency,
    int ItemCount);
```

Nếu project đang để records trong file riêng, tạo:

```text
Services/OrderingService/Presentation/Requests/DebugUpsertOrderSummaryRequest.cs
```

```csharp
namespace OrderingService.Presentation.Requests;

public sealed record DebugUpsertOrderSummaryRequest(
    Guid OrderId,
    Guid CustomerId,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    string Currency,
    int ItemCount);
```

Nếu project minimal API đang dùng trực tiếp trong `Program.cs`, có thể đặt record cuối file cho nhanh.

---

## 16. Tạo endpoints đọc read model

Trong `Program.cs`, thêm endpoints Development hoặc public read endpoint tùy project style.

Bản đơn giản:

```csharp
app.MapGet("/order-summaries", async (
    IOrderSummaryReadRepository repository,
    int limit,
    CancellationToken cancellationToken) =>
{
    if (limit <= 0 || limit > 100)
    {
        limit = 20;
    }

    var orders = await repository.GetLatestAsync(limit, cancellationToken);

    return Results.Ok(orders);
})
.WithTags("Order Summaries")
.WithName("GetOrderSummaries");

app.MapGet("/order-summaries/{orderId:guid}", async (
    Guid orderId,
    IOrderSummaryReadRepository repository,
    CancellationToken cancellationToken) =>
{
    var order = await repository.GetByOrderIdAsync(orderId, cancellationToken);

    return order is null
        ? Results.NotFound(new { message = "Order summary not found." })
        : Results.Ok(order);
})
.WithTags("Order Summaries")
.WithName("GetOrderSummaryByOrderId");
```

Debug seed endpoint:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapPost("/debug/order-summaries", async (
        DebugUpsertOrderSummaryRequest request,
        IOrderSummaryReadRepository repository,
        CancellationToken cancellationToken) =>
    {
        var now = DateTime.UtcNow;

        var model = new OrderSummaryReadModel
        {
            Id = request.OrderId,
            OrderId = request.OrderId,
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            Status = request.Status,
            TotalAmount = request.TotalAmount,
            Currency = request.Currency,
            ItemCount = request.ItemCount,
            CreatedAtUtc = now,
            LastUpdatedAtUtc = now
        };

        await repository.UpsertAsync(model, cancellationToken);

        return Results.Ok(model);
    })
    .WithTags("Debug")
    .WithName("DebugUpsertOrderSummary");
}
```

Cần using:

```csharp
using OrderingService.Application.ReadModels;
```

Nếu request record nằm namespace riêng:

```csharp
using OrderingService.Presentation.Requests;
```

---

## 17. Postman Lab

Collection:

```text
MicroShop - Lesson 26 MongoDB Read Model
```

Environment:

| Variable | Value |
| --- | --- |
| `ordering_url` | `http://localhost:5004` |

---

### Request 1: Debug upsert order summary

```text
POST {{ordering_url}}/debug/order-summaries
```

Body:

```json
{
  "orderId": "11111111-1111-1111-1111-111111111111",
  "customerId": "22222222-2222-2222-2222-222222222222",
  "customerName": "Demo Customer",
  "status": "Created",
  "totalAmount": 1977.3,
  "currency": "VND",
  "itemCount": 1
}
```

Expected:

```text
HTTP 200.
Response trả document OrderSummaryReadModel.
MongoDB có document trong collection order_summaries.
```

---

### Request 2: Get latest order summaries

```text
GET {{ordering_url}}/order-summaries?limit=20
```

Expected:

```text
HTTP 200.
Có danh sách order summaries.
Document vừa seed xuất hiện.
```

---

### Request 3: Get order summary by orderId

```text
GET {{ordering_url}}/order-summaries/11111111-1111-1111-1111-111111111111
```

Expected:

```text
HTTP 200.
Trả đúng order summary.
```

---

### Request 4: Get missing order summary

```text
GET {{ordering_url}}/order-summaries/99999999-9999-9999-9999-999999999999
```

Expected:

```text
HTTP 404.
message = Order summary not found.
```

---

### Request 5: Upsert cùng orderId lần hai

Gửi lại:

```text
POST {{ordering_url}}/debug/order-summaries
```

Body đổi status:

```json
{
  "orderId": "11111111-1111-1111-1111-111111111111",
  "customerId": "22222222-2222-2222-2222-222222222222",
  "customerName": "Demo Customer",
  "status": "Paid",
  "totalAmount": 1977.3,
  "currency": "VND",
  "itemCount": 1
}
```

Expected:

```text
Không tạo duplicate document cho cùng OrderId.
Status được update thành Paid.
```

Ý nghĩa:

```text
Upsert là nền tảng quan trọng cho ProjectionWorker bài 27.
```

---

## 18. Kiểm tra MongoDB bằng mongosh

Vào container:

```bash
docker exec -it microshop-mongodb mongosh -u microshop -p microshop
```

Trong shell:

```javascript
use MicroShop_OrderReadDb
show collections
db.order_summaries.find().pretty()
```

Expected:

```text
Thấy collection order_summaries.
Thấy document đã seed bằng Postman.
```

Thoát:

```text
exit
```

---

## 19. Index cho read model

Bản reviewed đã tạo index bằng code trong repository:

```text
UX_order_summaries_orderId
IX_order_summaries_createdAtUtc_desc
```

Vì sao cần 2 index này?

```text
GET /order-summaries/{orderId}
→ query theo OrderId
→ cần unique index theo OrderId.

GET /order-summaries?limit=20
→ sort latest theo CreatedAtUtc
→ cần index theo CreatedAtUtc descending.
```

Bạn vẫn có thể kiểm tra bằng mongosh:

```javascript
use MicroShop_OrderReadDb

db.order_summaries.getIndexes()
```

Expected:

```text
Có index _id_ mặc định.
Có UX_order_summaries_orderId.
Có IX_order_summaries_createdAtUtc_desc.
```

Câu nhớ:

```text
Read model phải có index theo query pattern.
```

---

## 20. Lỗi hay gặp

| Lỗi | Nguyên nhân | Cách xử lý |
| --- | --- | --- |
| Không connect được MongoDB | Container chưa chạy hoặc sai port | `docker compose up -d mongodb`, check port 27017 |
| Authentication failed | Sai username/password hoặc thiếu `authSource=admin` | Dùng `mongodb://microshop:microshop@localhost:27017/?authSource=admin` |
| Collection không có data | Chưa gọi debug seed endpoint | Gọi `POST /debug/order-summaries` |
| GET trả empty list | Seed vào database/collection khác | Kiểm tra `DatabaseName` và `OrderSummariesCollectionName` |
| Upsert tạo duplicate | Filter sai field hoặc Id khác OrderId | Check `Id = OrderId` và filter theo `x.Id` |
| Field trong MongoDB là PascalCase | Chưa cấu hình camelCase convention | Bài này chấp nhận PascalCase để giảm độ phức tạp |
| Decimal serialize lạ | MongoDB decimal handling khác SQL | Bài này chấp nhận; production có thể chuẩn hóa Decimal128 |
| 404 khi get by orderId | OrderId không khớp data seed | Copy đúng GUID |
| Endpoint debug không chạy | Không ở Development environment | Set `DOTNET_ENVIRONMENT=Development` |

---

## 21. Bài tập

### Bài 1: Giải thích read model

Trả lời:

```text
Read model là gì?
Vì sao không dùng luôn Orders table để trả mọi API đọc?
```

Gợi ý:

```text
Read model tối ưu cho query/UI.
Write model tối ưu cho nghiệp vụ/transaction.
```

---

### Bài 2: Test upsert

Dùng Postman:

```text
POST /debug/order-summaries
GET /order-summaries/{orderId}
```

Sau đó POST lại cùng `orderId` nhưng đổi `status`.

Ghi lại:

```text
Có tạo duplicate không?
Status có update không?
```

---

### Bài 3: Thiết kế thêm field

Bổ sung vào `OrderSummaryReadModel` 2 field:

```text
PaymentStatus
UpdatedReason
```

Trả lời:

```text
Hai field này có phù hợp read model không?
Có cần nằm trong write model không?
```

---

### Bài 4: Index theo query pattern

Trả lời:

```text
API nào query theo OrderId?
API nào query latest orders?
Nên có index nào?
```

Gợi ý:

```text
OrderId unique index.
CreatedAtUtc descending index nếu query latest nhiều.
```

---

### Bài 5: Chuẩn bị bài 27

Trả lời:

```text
Khi ProjectionWorker nhận OrderCreated event từ Kafka,
nó sẽ gọi method nào trong repository?
```

Đáp án kỳ vọng:

```text
UpsertAsync(OrderSummaryReadModel model)
```

---

## 22. Quiz nhanh

**Câu 1. Read model tối ưu cho điều gì?**

```text
A. Query/API response/UI view
B. Transaction nghiệp vụ phức tạp
C. Generate JWT
D. RabbitMQ routing
```

Đáp án: A

**Câu 2. Projection là gì?**

```text
A. Quá trình biến event/business data thành read model
B. Quá trình tạo Kafka partition
C. Quá trình validate JWT
D. Quá trình tạo Docker volume
```

Đáp án: A

**Câu 3. MongoDB trong bài này dùng để làm gì?**

```text
A. Read model/projection storage
B. Thay thế hoàn toàn write database của OrderingService
C. Lưu RabbitMQ queue
D. Là API Gateway
```

Đáp án: A

**Câu 4. Vì sao dùng upsert cho projection?**

```text
A. Event đến thì tạo mới hoặc update document hiện có theo OrderId
B. Để xóa toàn bộ collection trước mỗi event
C. Để Kafka tự commit offset
D. Để RabbitMQ không cần ack
```

Đáp án: A

**Câu 5. Buổi 26 có consume Kafka chưa?**

```text
A. Chưa, bài này chỉ chuẩn bị MongoDB read model
B. Có, đã làm ProjectionWorker hoàn chỉnh
C. Có, thay RabbitMQ bằng Kafka
D. Có, nhưng chỉ trong SQL Server
```

Đáp án: A

---

## 23. Production mindset

Bài này mới là foundation cho read model.

Đã có:

```text
MongoDB local.
OrderSummaryReadModel.
Repository upsert/get.
Read endpoints.
Debug seed endpoint.
```

Chưa có:

```text
Kafka consumer.
ProjectionWorker.
Replay strategy.
Idempotent projection nâng cao.
Schema migration cho read model.
Monitoring projection lag.
Rebuild read model job.
```

Điểm cần nhớ:

```text
Read model có thể stale trong một khoảng thời gian ngắn.
Đây là eventual consistency.
```

Ví dụ:

```text
Order đã tạo trong write DB.
Kafka event chưa được ProjectionWorker xử lý.
MongoDB read model chưa có order ngay lập tức.
```

Điều này không sai nếu hệ thống chấp nhận eventual consistency.

Câu nhớ:

```text
Read model nhanh cho query, nhưng phải chấp nhận đồng bộ bất đồng bộ.
```

---

## 23.1. Checkpoint tự vấn

Trước khi pass bài, tự trả lời nhanh:

```text
1. Vì sao MongoDB trong bài này không thay write DB của OrderingService?
2. Vì sao read model có thể stale vài giây mà vẫn chấp nhận được?
3. Vì sao bài này dùng Upsert thay vì Insert?
4. Vì sao Id = OrderId giúp projection dễ hơn?
5. Vì sao cần index theo OrderId và CreatedAtUtc?
6. Vì sao chưa consume Kafka trong bài 26?
7. Bài 27 sẽ dùng phần nào của bài 26?
```

Gợi ý đáp án:

```text
1. Write DB vẫn là source of truth nghiệp vụ.
2. Projection chạy async nên read side có eventual consistency.
3. Event đến có thể là create/update, upsert giúp idempotent hơn.
4. Mỗi order chỉ có một summary document.
5. Đây là query pattern của GET by id và latest list.
6. Bài 26 chỉ dựng read model storage; bài 27 mới nối Kafka.
7. ProjectionWorker sẽ gọi UpsertAsync vào MongoDB repository.
```

---

## 24. Điều kiện pass bài

Bạn pass Buổi 26 khi:

```text
[ ] MongoDB chạy bằng Docker Compose.
[ ] OrderingService connect được MongoDB.
[ ] Có OrderSummaryReadModel.
[ ] Có MongoDB repository.
[ ] Có endpoint `POST /debug/order-summaries`.
[ ] Có endpoint `GET /order-summaries`.
[ ] Có endpoint `GET /order-summaries/{orderId}`.
[ ] Postman seed được read model.
[ ] Postman đọc được read model.
[ ] Upsert cùng OrderId không tạo duplicate.
[ ] Bạn giải thích được read model khác write model.
[ ] Bạn giải thích được vì sao MongoDB dùng cho projection.
[ ] Bạn biết bài 27 sẽ nối Kafka event vào repository UpsertAsync.
```

Nếu hôm nay bạn chỉ nhớ một câu:

```text
MongoDB trong MicroShop là nơi chứa read model được build từ event, không phải source of truth nghiệp vụ.
```

là đã nắm xương sống bài 26.

---

## 25. Điều kiện mở khóa Buổi 27

Bạn có thể sang Buổi 27 khi:

```text
[ ] MongoDB local chạy ổn.
[ ] API đọc order summaries chạy được.
[ ] Repository UpsertAsync hoạt động.
[ ] Hiểu ProjectionWorker sẽ dùng Kafka event để gọi UpsertAsync.
```

Buổi 27 sẽ học:

```text
Kafka → MongoDB Projection
```

Mục tiêu Buổi 27:

```text
Tạo ProjectionWorker.
Consume Kafka topic microshop.order-events.
Map OrderCreated event thành OrderSummaryReadModel.
Upsert vào MongoDB.
Test produce Kafka event → MongoDB document được tạo/update.
```
