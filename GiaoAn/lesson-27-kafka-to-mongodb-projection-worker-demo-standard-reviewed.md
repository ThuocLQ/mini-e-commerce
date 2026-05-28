# Buổi 27: Kafka → MongoDB Projection - ProjectionWorker Demo

## 0. Vị trí trong lộ trình
Câu nhớ:
```text
RabbitMQ xử lý workflow.
Kafka nuôi projection/read model.
MongoDB lưu read model tối ưu cho query.
```
---

## 1. Mục tiêu bài học

Trong 90–120 phút, mục tiêu là:

```text
[ ] Tạo ProjectionWorker project.
[ ] Cài package Kafka consumer cho .NET.
[ ] Cấu hình Kafka consumer options.
[ ] Consume topic `microshop.order-events`.
[ ] Deserialize event JSON.
[ ] Upsert MongoDB `OrderSummaryReadModel`.
[ ] Test bằng Kafka CLI produce event.
[ ] Verify MongoDB read model bằng Postman.
[ ] Hiểu offset/consumer group trong ProjectionWorker.
[ ] Hiểu projection phải idempotent/rebuild-safe.
```

Output chính:

```text
Workers/ProjectionWorker
docs/projection-worker-notes.md
```

Bài này làm demo projection cơ bản.

Chưa làm:

```text
[ ] Chưa sửa OrderingService OutboxPublisher để publish Kafka.
[ ] Chưa dual-publish RabbitMQ + Kafka từ Outbox.
[ ] Chưa làm projection rebuild tool.
[ ] Chưa làm schema registry.
[ ] Chưa làm retry topic / DLT Kafka.
[ ] Chưa làm exactly-once.
```

Các phần production hơn để Stage 2.

Review note cho bản chuẩn:

```text
Bài này phải đặc biệt cẩn thận với Kafka offset.
Chỉ commit offset sau khi MongoDB update thành công.
Nếu xử lý fail, không được để consumer commit offset sau đó và nhảy qua message lỗi.
```

---

## 2. Target architecture sau bài 27

Trước bài 27:

```text
Kafka chạy local.
Topic microshop.order-events tồn tại.
MongoDB chạy local.
Có OrderSummaryReadModel.
```

Sau bài 27:

```text
Kafka topic microshop.order-events
    |
    v
ProjectionWorker
    |
    v
MongoDB order_summaries
    |
    v
GET /orders/read-model hoặc debug endpoint
```

Sơ đồ:

```text
Kafka
topic: microshop.order-events
        |
        v
ProjectionWorker
consumer group: projection-worker
        |
        v
MongoDB
collection: order_summaries
        |
        v
Postman / Read API
```

Ý nghĩa:

```text
Kafka là nguồn event stream.
ProjectionWorker chuyển event stream thành read model.
MongoDB lưu read model phục vụ query nhanh/dễ đọc.
```

---

## 3. Projection là gì?

Projection là quá trình biến event thành read model.

Ví dụ event:

```json
{
  "eventId": "11111111-1111-1111-1111-111111111111",
  "eventType": "OrderCreated",
  "orderId": "ORD-001",
  "customerId": "CUST-001",
  "totalAmount": 1977.3,
  "currency": "VND",
  "occurredAtUtc": "2026-05-27T10:00:00Z"
}
```

Projection tạo read model:

```json
{
  "orderId": "ORD-001",
  "customerId": "CUST-001",
  "status": "Created",
  "totalAmount": 1977.3,
  "currency": "VND",
  "createdAtUtc": "2026-05-27T10:00:00Z",
  "lastUpdatedAtUtc": "2026-05-27T10:00:00Z"
}
```

Nếu sau đó có event:

```json
{
  "eventType": "OrderPaid",
  "orderId": "ORD-001",
  "occurredAtUtc": "2026-05-27T10:05:00Z"
}
```

Projection update read model:

```json
{
  "orderId": "ORD-001",
  "status": "Paid",
  "lastUpdatedAtUtc": "2026-05-27T10:05:00Z"
}
```

Câu nhớ:

```text
Event là chuyện đã xảy ra.
Projection là trạng thái đọc được sinh ra từ event.
```

---

## 4. Vì sao cần ProjectionWorker?

Nếu đọc dữ liệu trực tiếp từ write model:

```text
OrderingService DB
→ join nhiều bảng
→ query theo màn hình phức tạp
```

Với read model:

```text
ProjectionWorker build sẵn document cho màn hình.
API đọc MongoDB nhanh hơn và đơn giản hơn.
```

Ví dụ màn hình order summary cần:

```text
OrderId
CustomerId
Status
TotalAmount
Currency
CreatedAtUtc
PaidAtUtc
CancelledAtUtc
LastUpdatedAtUtc
```

Thay vì query nhiều bảng, read model có thể lưu sẵn một document.

Câu nhớ:

```text
Write model tối ưu cho consistency/transaction.
Read model tối ưu cho query/screen.
```

---

## 5. Scope guard của bài 27

Bài này làm:

```text
[ ] ProjectionWorker consume Kafka.
[ ] Parse JSON order events.
[ ] Upsert MongoDB read model.
[ ] Test bằng Kafka CLI + Postman.
```

Không làm:

```text
[ ] Không publish Kafka từ OrderingService.
[ ] Không thay RabbitMQ bằng Kafka.
[ ] Không bỏ NotificationWorker.
[ ] Không làm Kafka retry topic/DLT.
[ ] Không làm projection rebuild command.
[ ] Không làm multiple projections.
[ ] Không làm OpenTelemetry.
```

Lý do:

```text
Bài 27 chỉ chứng minh Kafka → MongoDB projection flow.
Sau này mới nâng cấp production.
```

---

## 6. Chuẩn bị trước khi làm

Bạn cần có từ bài 25:

```text
[ ] Kafka chạy local.
[ ] Topic `microshop.order-events` tồn tại.
[ ] Produce/consume được bằng Kafka CLI.
```

Bạn cần có từ bài 26:

```text
[ ] MongoDB chạy local.
[ ] Có OrderSummaryReadModel.
[ ] Có Mongo repository hoặc code mẫu để upsert/read.
[ ] Có endpoint/debug endpoint đọc MongoDB bằng Postman.
```

Chạy infra:

```bash
docker compose up -d zookeeper kafka mongodb
```

Nếu service MongoDB trong compose đặt tên khác, dùng đúng tên hiện tại.

Kiểm tra topic:

```powershell
docker exec -it microshop-kafka kafka-topics --bootstrap-server localhost:9092 --list
```

Nếu chưa có topic:

```powershell
docker exec -it microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
```

---

## 7. Tạo ProjectionWorker project

Tạo folder:

```text
Workers
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force Workers
```

Tạo Worker Service:

```powershell
dotnet new worker -n ProjectionWorker -o Workers/ProjectionWorker
```

Add vào solution:

```powershell
dotnet sln add Workers/ProjectionWorker/ProjectionWorker.csproj
```

Add reference nếu cần dùng shared contracts/read model abstractions:

```powershell
dotnet add Workers/ProjectionWorker/ProjectionWorker.csproj reference BuildingBlocks/BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj
```

Trong bài này có 2 lựa chọn:

```text
Option A: ProjectionWorker tự định nghĩa Kafka DTO JSON đơn giản.
Option B: ProjectionWorker dùng shared contract từ BuildingBlocks.Contracts.
```

Để học nhanh và ít coupling hơn với event JSON thủ công, bài này dùng:

```text
Option A: Kafka DTO riêng trong ProjectionWorker.
```

Lý do:

```text
Kafka message trong bài này được produce bằng CLI.
Payload có field eventType.
Ta chưa tích hợp OrderingService publish Kafka.
```

Sau này khi OrderingService publish Kafka thật, có thể chuẩn hóa contract/envelope lại.

---

## 8. Cài packages

Vào project:

```powershell
cd Workers/ProjectionWorker
```

Cài Kafka client:

```powershell
dotnet add package Confluent.Kafka
```

Cài MongoDB driver:

```powershell
dotnet add package MongoDB.Driver
```

Quay lại root solution:

```powershell
cd ../..
```

Build:

```powershell
dotnet build Workers/ProjectionWorker/ProjectionWorker.csproj
```

Vì sao repository đăng ký singleton?

```text
IMongoClient thread-safe và nên dùng singleton.
Repository giữ IMongoCollection và tạo index một lần theo process.
Không cần tạo scope mới cho mỗi Kafka message trong bài basic này.
```

---

## 9. Cấu hình appsettings.Development.json

Tạo hoặc sửa file:

```text
Workers/ProjectionWorker/appsettings.Development.json
```

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Topic": "microshop.order-events",
    "GroupId": "projection-worker",
    "AutoOffsetReset": "Earliest"
  },
  "MongoDb": {
    "ConnectionString": "mongodb://microshop:microshop@localhost:27017/?authSource=admin",
    "DatabaseName": "microshop_read",
    "OrderSummariesCollectionName": "order_summaries"
  }
}
```

Ý nghĩa:

| Setting | Ý nghĩa |
| --- | --- |
| `BootstrapServers` | Kafka endpoint từ host |
| `Topic` | Topic cần consume |
| `GroupId` | Consumer group của ProjectionWorker |
| `AutoOffsetReset` | Nếu group chưa có offset thì đọc từ earliest |
| `ConnectionString` | Kết nối MongoDB |
| `DatabaseName` | Database read model |
| `OrderSummariesCollectionName` | Collection order summaries |

Lưu ý:

```text
AutoOffsetReset=Earliest chỉ có tác dụng khi group chưa có committed offset.
Nếu group đã đọc rồi, đổi group id mới để đọc lại từ đầu.
```

---

## 10. Tạo Options classes

Tạo folder:

```text
Workers/ProjectionWorker/Options
```

Tạo file:

```text
Workers/ProjectionWorker/Options/KafkaOptions.cs
```

```csharp
namespace ProjectionWorker.Options;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; init; } = "localhost:9092";
    public string Topic { get; init; } = "microshop.order-events";
    public string GroupId { get; init; } = "projection-worker";
    public string AutoOffsetReset { get; init; } = "Earliest";
}
```

Tạo file:

```text
Workers/ProjectionWorker/Options/MongoDbOptions.cs
```

```csharp
namespace ProjectionWorker.Options;

public sealed class MongoDbOptions
{
    public string ConnectionString { get; init; } = default!;
    public string DatabaseName { get; init; } = "microshop_read";
    public string OrderSummariesCollectionName { get; init; } = "order_summaries";
}
```

---

## 11. Tạo Kafka event DTO

Tạo folder:

```text
Workers/ProjectionWorker/KafkaEvents
```

Tạo file:

```text
Workers/ProjectionWorker/KafkaEvents/OrderEventMessage.cs
```

```csharp
namespace ProjectionWorker.KafkaEvents;

public sealed class OrderEventMessage
{
    public Guid EventId { get; init; }
    public string EventType { get; init; } = default!;
    public string OrderId { get; init; } = default!;
    public string CustomerId { get; init; } = default!;
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "VND";
    public DateTime OccurredAtUtc { get; init; }
}
```

Bài này hỗ trợ các `EventType`:

```text
OrderCreated
OrderPaid
OrderCancelled
```

Câu nhớ:

```text
Kafka event nên có eventId, eventType, aggregate id, occurredAtUtc.
```

---

## 12. Tạo MongoDB read model

Tạo folder:

```text
Workers/ProjectionWorker/ReadModels
```

Tạo file:

```text
Workers/ProjectionWorker/ReadModels/OrderSummaryReadModel.cs
```

```csharp
namespace ProjectionWorker.ReadModels;

public sealed class OrderSummaryReadModel
{
    public string Id { get; set; } = default!;
    public string OrderId { get; set; } = default!;
    public string CustomerId { get; set; } = default!;
    public string Status { get; set; } = "Created";
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime LastUpdatedAtUtc { get; set; }
}
```

Trong bài này:

```text
Id = OrderId
```

Lý do:

```text
Mỗi order có một summary document.
Upsert theo OrderId dễ idempotent hơn.
```

---

## 13. Tạo repository MongoDB

Tạo folder:

```text
Workers/ProjectionWorker/Persistence
```

Tạo file:

```text
Workers/ProjectionWorker/Persistence/IOrderSummaryRepository.cs
```

```csharp
using ProjectionWorker.KafkaEvents;

namespace ProjectionWorker.Persistence;

public interface IOrderSummaryRepository
{
    Task ApplyAsync(
        OrderEventMessage message,
        CancellationToken cancellationToken = default);
}
```

Tạo file:

```text
Workers/ProjectionWorker/Persistence/MongoOrderSummaryRepository.cs
```

```csharp
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProjectionWorker.KafkaEvents;
using ProjectionWorker.Options;
using ProjectionWorker.ReadModels;

namespace ProjectionWorker.Persistence;

public sealed class MongoOrderSummaryRepository : IOrderSummaryRepository
{
    private readonly IMongoCollection<OrderSummaryReadModel> _collection;
    private readonly ILogger<MongoOrderSummaryRepository> _logger;

    public MongoOrderSummaryRepository(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options,
        ILogger<MongoOrderSummaryRepository> logger)
    {
        _logger = logger;

        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        _collection = database.GetCollection<OrderSummaryReadModel>(
            options.Value.OrderSummariesCollectionName);

        EnsureIndexes();
    }

    public async Task ApplyAsync(
        OrderEventMessage message,
        CancellationToken cancellationToken = default)
    {
        switch (message.EventType)
        {
            case "OrderCreated":
                await ApplyOrderCreatedAsync(message, cancellationToken);
                break;

            case "OrderPaid":
                await ApplyOrderPaidAsync(message, cancellationToken);
                break;

            case "OrderCancelled":
                await ApplyOrderCancelledAsync(message, cancellationToken);
                break;

            default:
                _logger.LogWarning(
                    "Unsupported order event type. EventType={EventType}, EventId={EventId}",
                    message.EventType,
                    message.EventId);
                break;
        }
    }

    private async Task ApplyOrderCreatedAsync(
        OrderEventMessage message,
        CancellationToken cancellationToken)
    {
        var filter = Builders<OrderSummaryReadModel>.Filter.Eq(x => x.OrderId, message.OrderId);

        var update = Builders<OrderSummaryReadModel>.Update
            .SetOnInsert(x => x.Id, message.OrderId)
            .SetOnInsert(x => x.OrderId, message.OrderId)
            .SetOnInsert(x => x.Status, "Created")
            .SetOnInsert(x => x.CreatedAtUtc, message.OccurredAtUtc)
            .SetOnInsert(x => x.LastUpdatedAtUtc, message.OccurredAtUtc)
            .Set(x => x.CustomerId, message.CustomerId)
            .Set(x => x.TotalAmount, message.TotalAmount)
            .Set(x => x.Currency, message.Currency);

        await _collection.UpdateOneAsync(
            filter,
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    private async Task ApplyOrderPaidAsync(
        OrderEventMessage message,
        CancellationToken cancellationToken)
    {
        var filter = Builders<OrderSummaryReadModel>.Filter.Eq(x => x.OrderId, message.OrderId);

        var update = Builders<OrderSummaryReadModel>.Update
            .Set(x => x.Status, "Paid")
            .Set(x => x.PaidAtUtc, message.OccurredAtUtc)
            .Set(x => x.LastUpdatedAtUtc, message.OccurredAtUtc);

        await _collection.UpdateOneAsync(
            filter,
            update,
            new UpdateOptions { IsUpsert = false },
            cancellationToken);
    }

    private async Task ApplyOrderCancelledAsync(
        OrderEventMessage message,
        CancellationToken cancellationToken)
    {
        var filter = Builders<OrderSummaryReadModel>.Filter.Eq(x => x.OrderId, message.OrderId);

        var update = Builders<OrderSummaryReadModel>.Update
            .Set(x => x.Status, "Cancelled")
            .Set(x => x.CancelledAtUtc, message.OccurredAtUtc)
            .Set(x => x.LastUpdatedAtUtc, message.OccurredAtUtc);

        await _collection.UpdateOneAsync(
            filter,
            update,
            new UpdateOptions { IsUpsert = false },
            cancellationToken);
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

        var createdAtIndex = new CreateIndexModel<OrderSummaryReadModel>(
            Builders<OrderSummaryReadModel>.IndexKeys.Descending(x => x.CreatedAtUtc),
            new CreateIndexOptions
            {
                Name = "IX_order_summaries_createdAtUtc_desc"
            });

        _collection.Indexes.CreateMany(new[] { orderIdIndex, createdAtIndex });
    }
}
```

Điểm cần hiểu:

```text
OrderCreated dùng upsert vì có thể replay event.
OrderCreated dùng SetOnInsert cho Status/CreatedAtUtc/LastUpdatedAtUtc để tránh replay OrderCreated làm tụt status từ Paid về Created.
OrderPaid/OrderCancelled update document đã có.
Nếu event Paid đến trước Created, bài này chưa xử lý out-of-order nâng cao.
```

Production hơn sẽ cần:

```text
Event ordering theo key.
Version/checkpoint.
Out-of-order handling.
Idempotency theo EventId.
Projection rebuild strategy.
```

---

## 14. Tạo Kafka consumer worker

Mở file mặc định:

```text
Workers/ProjectionWorker/Worker.cs
```

Thay bằng:

```csharp
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using ProjectionWorker.KafkaEvents;
using ProjectionWorker.Options;
using ProjectionWorker.Persistence;

namespace ProjectionWorker;

public sealed class Worker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<Worker> _logger;
    private readonly IOrderSummaryRepository _repository;
    private readonly KafkaOptions _options;

    public Worker(
        ILogger<Worker> logger,
        IOrderSummaryRepository repository,
        IOptions<KafkaOptions> options)
    {
        _logger = logger;
        _repository = repository;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            AutoOffsetReset = ParseAutoOffsetReset(_options.AutoOffsetReset),
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        consumer.Subscribe(_options.Topic);

        _logger.LogInformation(
            "ProjectionWorker started. Topic={Topic}, GroupId={GroupId}, BootstrapServers={BootstrapServers}",
            _options.Topic,
            _options.GroupId,
            _options.BootstrapServers);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;

                try
                {
                    result = consumer.Consume(stoppingToken);

                    await HandleMessageAsync(result, stoppingToken);

                    consumer.Commit(result);

                    _logger.LogInformation(
                        "Kafka message processed and committed. Topic={Topic}, Partition={Partition}, Offset={Offset}, Key={Key}",
                        result.Topic,
                        result.Partition.Value,
                        result.Offset.Value,
                        result.Message.Key);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(
                        ex,
                        "Kafka consume error. Reason={Reason}",
                        ex.Error.Reason);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // App is shutting down.
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to process Kafka message. Topic={Topic}, Partition={Partition}, Offset={Offset}",
                        result?.Topic,
                        result?.Partition.Value,
                        result?.Offset.Value);

                    if (result is not null)
                    {
                        // Basic demo behavior:
                        // Seek back to the failed message so this worker does not skip it
                        // and later commit a higher offset from the same partition.
                        consumer.Seek(result.TopicPartitionOffset);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task HandleMessageAsync(
        ConsumeResult<string, string> result,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<OrderEventMessage>(
            result.Message.Value,
            JsonOptions);

        if (message is null)
        {
            throw new InvalidOperationException("Cannot deserialize Kafka message to OrderEventMessage.");
        }

        await _repository.ApplyAsync(message, cancellationToken);
    }

    private static AutoOffsetReset ParseAutoOffsetReset(string value)
    {
        return value.Equals("Latest", StringComparison.OrdinalIgnoreCase)
            ? AutoOffsetReset.Latest
            : AutoOffsetReset.Earliest;
    }
}
```

Điểm quan trọng:

```text
EnableAutoCommit = false.
EnableAutoOffsetStore = false.
Chỉ Commit offset sau khi upsert MongoDB thành công.
Nếu xử lý fail, Seek về lại offset lỗi để không nhảy qua message đó.
```

Vì sao cần `Seek` khi fail?

```text
Kafka consumer có thể tiếp tục đọc message phía sau trong cùng partition.
Nếu message sau được commit, offset có thể vượt qua message lỗi.
Bản demo này seek lại offset lỗi để giữ đúng nguyên tắc không skip message fail.
```

Câu nhớ:

```text
Commit offset sau khi side effect thành công.
Fail thì không được commit vượt qua message lỗi.
```

Lưu ý:

```text
Bài này vẫn là basic.
Nếu message luôn lỗi, worker sẽ retry mãi message đó và block partition.
Production cần retry topic / DLT / poison handling.
```

---

## 15. Register services trong Program.cs

Mở:

```text
Workers/ProjectionWorker/Program.cs
```

Sửa thành:

```csharp
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProjectionWorker;
using ProjectionWorker.Options;
using ProjectionWorker.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection("Kafka"));

builder.Services.Configure<MongoDbOptions>(
    builder.Configuration.GetSection("MongoDb"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
    return new MongoClient(options.ConnectionString);
});

builder.Services.AddSingleton<IOrderSummaryRepository, MongoOrderSummaryRepository>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

Build:

```powershell
dotnet build Workers/ProjectionWorker/ProjectionWorker.csproj
```

---

## 16. Chạy ProjectionWorker

Set environment:

```powershell
$env:DOTNET_ENVIRONMENT="Development"
dotnet run --project Workers/ProjectionWorker/ProjectionWorker.csproj
```

Expected log:

```text
ProjectionWorker started. Topic=microshop.order-events, GroupId=projection-worker, BootstrapServers=localhost:9092
```

Nếu lỗi connect Kafka:

```text
Kiểm tra Kafka container.
Kiểm tra localhost:9092.
Kiểm tra advertised listeners từ bài 25.
```

Nếu lỗi MongoDB auth:

```text
Kiểm tra connection string có authSource=admin.
Kiểm tra mongodb container đang chạy.
```

---

## 17. Produce event test bằng Kafka CLI

Mở producer:

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

Gửi OrderCreated:

```text
ORD-900:{"eventId":"11111111-1111-1111-1111-111111111111","eventType":"OrderCreated","orderId":"ORD-900","customerId":"CUST-900","totalAmount":1977.3,"currency":"VND","occurredAtUtc":"2026-05-27T10:00:00Z"}
```

Expected ProjectionWorker log:

```text
Kafka message processed. Topic=microshop.order-events, Partition=..., Offset=..., Key=ORD-900
```

Gửi OrderPaid:

```text
ORD-900:{"eventId":"22222222-2222-2222-2222-222222222222","eventType":"OrderPaid","orderId":"ORD-900","customerId":"CUST-900","totalAmount":1977.3,"currency":"VND","occurredAtUtc":"2026-05-27T10:05:00Z"}
```

Expected:

```text
MongoDB document ORD-900 status chuyển thành Paid.
```

Gửi OrderCancelled test order khác:

```text
ORD-901:{"eventId":"33333333-3333-3333-3333-333333333333","eventType":"OrderCreated","orderId":"ORD-901","customerId":"CUST-901","totalAmount":500,"currency":"VND","occurredAtUtc":"2026-05-27T10:10:00Z"}
```

```text
ORD-901:{"eventId":"44444444-4444-4444-4444-444444444444","eventType":"OrderCancelled","orderId":"ORD-901","customerId":"CUST-901","totalAmount":500,"currency":"VND","occurredAtUtc":"2026-05-27T10:15:00Z"}
```

Expected:

```text
MongoDB document ORD-901 status = Cancelled.
```

---

## 18. Verify MongoDB bằng Postman

Nếu bài 26 đã có endpoint đọc read model, gọi:

```text
GET {{ordering_url}}/orders/read-model
```

hoặc endpoint bạn đã tạo ở bài 26.

Expected có:

```json
[
  {
    "orderId": "ORD-900",
    "customerId": "CUST-900",
    "status": "Paid",
    "totalAmount": 1977.3,
    "currency": "VND"
  },
  {
    "orderId": "ORD-901",
    "customerId": "CUST-901",
    "status": "Cancelled",
    "totalAmount": 500,
    "currency": "VND"
  }
]
```

Nếu chưa có endpoint từ bài 26, dùng MongoDB shell tạm:

```powershell
docker exec -it microshop-mongodb mongosh -u microshop -p microshop --authenticationDatabase admin
```

Trong shell:

```javascript
use microshop_read
db.order_summaries.find().pretty()
```

---

## 19. Test consumer group offset

Xem group:

```powershell
docker exec -it microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Expected:

```text
CURRENT-OFFSET tăng sau khi worker xử lý thành công.
LAG giảm về 0 nếu không còn message chờ.
```

Nếu worker fail xử lý message:

```text
Offset không commit.
Message có thể được xử lý lại sau restart.
```

Câu nhớ:

```text
MongoDB upsert thành công rồi mới commit Kafka offset.
```

---

## 20. Test replay bằng group mới

Dừng ProjectionWorker.

Đổi config:

```json
"GroupId": "projection-worker-replay-demo"
```

Chạy lại:

```powershell
$env:DOTNET_ENVIRONMENT="Development"
dotnet run --project Workers/ProjectionWorker/ProjectionWorker.csproj
```

Vì group mới chưa có offset và `AutoOffsetReset=Earliest`, worker sẽ đọc lại event từ đầu nếu event còn trong Kafka retention.

Expected:

```text
ProjectionWorker xử lý lại event cũ.
MongoDB không tạo duplicate document vì upsert theo OrderId.
```

Câu nhớ:

```text
Replay chỉ an toàn khi projection idempotent hoặc rebuild-safe.
```

---

## 21. Idempotency trong projection

Bài này idempotent ở mức document:

```text
OrderCreated cùng OrderId được apply lại
→ Upsert cùng document
→ Không tạo duplicate document
```

Nhưng chưa idempotent đầy đủ theo EventId.

Ví dụ:

```text
OrderPaid EventId=E1 apply xong.
Replay EventId=E1 lần nữa.
Status vẫn Paid.
```

Case này ổn.

Nhưng production cần nghĩ thêm:

```text
Event cũ đến sau event mới.
Duplicate event với payload khác.
EventId đã xử lý chưa?
Projection có cần processed_events collection không?
```

Stage 2 sẽ học sâu hơn:

```text
Inbox / ProcessedMessages.
Projection rebuild.
Idempotency nâng cao.
```

---

## 22. Out-of-order event là gì?

Out-of-order nghĩa là event đến không đúng thứ tự nghiệp vụ.

Ví dụ:

```text
OrderPaid đến trước OrderCreated.
```

Trong bài này:

```text
OrderPaid update document với IsUpsert=false.
Nếu document chưa tồn tại, update không làm gì.
Sau đó OrderCreated đến, status lại Created.
```

Đây là limitation của bài basic.

Vì sao vẫn chấp nhận hôm nay?

```text
Bài 27 chỉ demo Kafka → MongoDB projection.
Ta sẽ dùng key = OrderId để tăng khả năng event cùng order vào cùng partition.
Kafka giữ ordering trong cùng partition.
Nếu producer gửi đúng order và key đúng, thứ tự theo order ổn hơn.
```

Production cần thêm:

```text
Version/sequence per aggregate.
Event timestamp comparison.
Rebuild strategy.
Dead letter/parking lot cho event không apply được.
```

Câu nhớ:

```text
Kafka giúp ordering trong partition, không tự giải quyết mọi out-of-order business case.
```

---

## 23. Lỗi hay gặp

| Lỗi | Nguyên nhân | Cách xử lý |
| --- | --- | --- |
| Worker không nhận event cũ | Group đã có committed offset | Đổi GroupId mới hoặc reset offset |
| Worker không nhận event mới | Sai topic hoặc Kafka chưa chạy | Check topic, producer, Kafka logs |
| `AutoOffsetReset=Earliest` nhưng không đọc lại | Setting chỉ áp dụng khi group chưa có offset | Đổi group id mới |
| Mongo auth failed | Thiếu `authSource=admin` | Sửa connection string |
| Document không update Paid/Cancelled | Chưa có OrderCreated trước đó | Gửi OrderCreated trước hoặc xử lý out-of-order sau |
| Duplicate document | Không upsert theo OrderId hoặc thiếu unique index | Check repository/index |
| Worker đứng ở một message lỗi | Worker seek lại offset lỗi và không commit | Đây là expected basic để không mất message; production cần retry topic/DLT |
| JSON deserialize fail | Payload sai field/type/date format | Check Kafka message JSON |
| Consumer group lag cao | Worker xử lý chậm hoặc đang fail | Check logs và `kafka-consumer-groups --describe` |

---

## 24. Tạo docs projection-worker-notes.md

Tạo file:

```text
docs/projection-worker-notes.md
```

Nội dung gợi ý:

```md
# ProjectionWorker Notes

## Purpose

ProjectionWorker consumes order events from Kafka and updates MongoDB read model.

## Input

Kafka topic:

```text
microshop.order-events
```

Supported event types:

```text
OrderCreated
OrderPaid
OrderCancelled
```

## Output

MongoDB:

```text
Database: microshop_read
Collection: order_summaries
```

## Consumer Group

```text
projection-worker
```

## Offset Strategy

Auto commit is disabled.

The worker commits Kafka offset only after MongoDB update succeeds.

## Idempotency

Current basic idempotency:

- Upsert by OrderId.
- Replaying OrderCreated does not create duplicate document.

Limitations:

- No processed EventId collection.
- Out-of-order events are not fully handled.
- No Kafka DLT/retry topic.
- No rebuild command.

## Rule

Kafka event stream feeds MongoDB read model.

RabbitMQ still handles workflow/task processing such as NotificationWorker.
```

---

## 25. Postman Lab

Collection:

```text
MicroShop - Lesson 27 Kafka MongoDB Projection
```

Environment:

| Variable | Value |
| --- | --- |
| `ordering_url` | `http://localhost:5004` |

### Request 1: Verify read model before Kafka event

```text
GET {{ordering_url}}/orders/read-model
```

Expected:

```text
Có thể chưa có ORD-900.
```

### CLI Step: Produce OrderCreated

```text
ORD-900:{"eventId":"11111111-1111-1111-1111-111111111111","eventType":"OrderCreated","orderId":"ORD-900","customerId":"CUST-900","totalAmount":1977.3,"currency":"VND","occurredAtUtc":"2026-05-27T10:00:00Z"}
```

### Request 2: Verify Created

```text
GET {{ordering_url}}/orders/read-model
```

Expected:

```text
ORD-900 status = Created.
```

### CLI Step: Produce OrderPaid

```text
ORD-900:{"eventId":"22222222-2222-2222-2222-222222222222","eventType":"OrderPaid","orderId":"ORD-900","customerId":"CUST-900","totalAmount":1977.3,"currency":"VND","occurredAtUtc":"2026-05-27T10:05:00Z"}
```

### Request 3: Verify Paid

```text
GET {{ordering_url}}/orders/read-model
```

Expected:

```text
ORD-900 status = Paid.
PaidAtUtc có giá trị.
```

### Request 4: Check replay

Đổi ProjectionWorker GroupId:

```text
projection-worker-replay-demo
```

Restart worker.

Gọi lại:

```text
GET {{ordering_url}}/orders/read-model
```

Expected:

```text
Không có duplicate ORD-900.
Document vẫn là một record theo OrderId.
```

---

## 26. Bài tập

### Bài 1: Vẽ projection flow

Vẽ:

```text
Kafka topic
→ ProjectionWorker
→ MongoDB order_summaries
→ Read API/Postman
```

Giải thích:

```text
ProjectionWorker làm gì?
MongoDB read model khác write DB thế nào?
```

---

### Bài 2: Test 3 event

Produce:

```text
OrderCreated ORD-1000
OrderPaid ORD-1000
OrderCreated ORD-1001
```

Verify MongoDB:

```text
ORD-1000 status = Paid.
ORD-1001 status = Created.
```

---

### Bài 3: Test replay

Đổi consumer group mới.

Chạy lại ProjectionWorker.

Trả lời:

```text
Vì sao worker đọc lại event cũ?
Vì sao MongoDB không tạo duplicate document?
```

---

### Bài 4: Test out-of-order

Produce:

```text
OrderPaid ORD-2000
OrderCreated ORD-2000
```

Quan sát result.

Trả lời:

```text
Status cuối là gì?
Vì sao đây là limitation?
Production nên xử lý bằng gì?
```

---

### Bài 5: Offset commit

Trả lời:

```text
Vì sao nên commit offset sau khi MongoDB update thành công?
Nếu commit trước rồi MongoDB fail thì rủi ro gì?
Nếu MongoDB thành công nhưng commit fail thì rủi ro gì?
```

Gợi ý:

```text
Commit trước rồi Mongo fail → mất cơ hội xử lý lại event.
Mongo thành công nhưng commit fail → event xử lý lại, cần idempotency.
```

---

## 27. Quiz nhanh

**Câu 1. ProjectionWorker làm gì?**

```text
A. Consume Kafka event và update MongoDB read model
B. Publish RabbitMQ notification
C. Tạo JWT token
D. Route request qua API Gateway
```

Đáp án: A

**Câu 2. Vì sao commit Kafka offset sau khi update MongoDB thành công?**

```text
A. Để tránh mất event nếu update MongoDB fail
B. Để Kafka tự xóa topic
C. Để RabbitMQ tạo error queue
D. Để MongoDB tự tạo partition
```

Đáp án: A

**Câu 3. Replay bằng consumer group mới có ý nghĩa gì?**

```text
A. Group mới chưa có offset nên có thể đọc lại event từ đầu nếu retention còn
B. Kafka sẽ duplicate topic
C. RabbitMQ sẽ gửi lại message cũ
D. MongoDB sẽ xóa collection
```

Đáp án: A

**Câu 4. Vì sao upsert theo OrderId giúp projection idempotent cơ bản?**

```text
A. Replay cùng order update cùng document thay vì tạo document mới
B. Kafka không bao giờ gửi duplicate
C. MongoDB không cần index
D. Consumer không cần deserialize JSON
```

Đáp án: A

**Câu 5. Bài 27 chưa xử lý production đầy đủ điểm nào?**

```text
A. Out-of-order event, processed EventId, retry topic/DLT, rebuild strategy
B. Tạo Kafka topic
C. Consume Kafka message
D. Update MongoDB document
```

Đáp án: A

---

## 28. Production mindset

Bài 27 giúp bạn hiểu event-driven read model:

```text
Event stream không chỉ dùng để trigger side effect.
Event stream còn dùng để build projection/read model.
```

Đã có:

```text
ProjectionWorker.
Kafka consumer group.
MongoDB upsert read model.
Manual offset commit sau xử lý thành công.
Replay bằng group mới.
Idempotency cơ bản theo OrderId.
```

Chưa có:

```text
Kafka publish từ OrderingService Outbox.
Event envelope chuẩn.
Schema versioning.
Processed EventId collection.
Out-of-order handling.
Retry topic / DLT.
Projection rebuild command.
Metrics/lag monitoring dashboard.
```

Câu nhớ:

```text
Projection phải chịu được replay.
Replay chỉ an toàn khi projection idempotent hoặc rebuild-safe.
```

---

## 28.1. Checkpoint tự vấn trước khi pass

Tự trả lời nhanh:

```text
1. Vì sao ProjectionWorker không commit offset trước khi update MongoDB?
2. Nếu MongoDB update thành công nhưng commit offset fail thì chuyện gì xảy ra?
3. Vì sao replay bằng group mới không được tạo duplicate document?
4. Vì sao OrderCreated dùng SetOnInsert cho Status/CreatedAtUtc?
5. Vì sao message poison có thể block một partition trong bản basic?
6. Kafka key = OrderId giúp gì cho projection?
```

Gợi ý đáp án:

```text
1. Commit trước rồi Mongo fail sẽ mất cơ hội xử lý lại event.
2. Event có thể xử lý lại; projection phải idempotent.
3. Vì upsert theo OrderId, mỗi order chỉ một document.
4. Để replay OrderCreated không làm tụt status từ Paid/Cancelled về Created.
5. Vì worker seek lại offset lỗi và không commit vượt qua message đó.
6. Giúp event cùng order vào cùng partition, giữ ordering theo order tốt hơn.
```

---

## 29. Điều kiện pass bài

Bạn pass Buổi 27 khi:

```text
[ ] Tạo được ProjectionWorker.
[ ] ProjectionWorker connect Kafka được.
[ ] ProjectionWorker connect MongoDB được.
[ ] Consume được topic `microshop.order-events`.
[ ] Produce OrderCreated bằng Kafka CLI.
[ ] MongoDB có document order summary.
[ ] Produce OrderPaid.
[ ] MongoDB status chuyển Paid.
[ ] Produce OrderCancelled cho order khác.
[ ] MongoDB status chuyển Cancelled.
[ ] Xem được consumer group offset/lag.
[ ] Test được replay bằng group mới.
[ ] Không tạo duplicate document khi replay.
[ ] Giải thích được vì sao commit offset sau MongoDB update.
[ ] Giải thích được limitation out-of-order.
```

Nếu hôm nay chỉ làm được flow này là đạt mục tiêu chính:

```text
Kafka CLI produce event
→ ProjectionWorker consume
→ MongoDB upsert
→ Postman/read API thấy read model
```

---

## 30. Điều kiện mở khóa Buổi 28

Bạn có thể sang Buổi 28 khi:

```text
[ ] Hiểu Kafka → MongoDB projection.
[ ] ProjectionWorker chạy được.
[ ] MongoDB read model update theo event.
[ ] Biết replay cần idempotency.
[ ] Biết RabbitMQ và Kafka đang phục vụ hai mục đích khác nhau.
```

Buổi 28 sẽ học:

```text
Logging + Health Checks
```

Mục tiêu Buổi 28:

```text
Thêm health checks.
Structured logs cơ bản.
Chuẩn bị runbook intro.
Giúp MicroShop quan sát được service sống/chết trước khi sang docs/demo cuối Stage 1.
```
