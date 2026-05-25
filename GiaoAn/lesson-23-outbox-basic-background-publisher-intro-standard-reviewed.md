# Buổi 23: Outbox Basic + Background Publisher Intro

## 0. Vị trí trong lộ trình

Theo roadmap Phase 1.5:

```text
Buổi 19: RabbitMQ + MassTransit
Buổi 20: BuildingBlocks.Contracts + Event Design
Buổi 21: NotificationWorker
Buổi 22: Retry/DLQ + Idempotency Basic
Buổi 23: Outbox Basic + Background Publisher Intro
```

Bạn đã xong Buổi 22:

```text
NotificationWorker consume OrderCreatedIntegrationEvent.
Consumer có retry policy.
Consumer fail sau retry thì message vào error queue.
Consumer có idempotency basic theo EventId.
```

Flow hiện tại:

```text
POST /orders
→ OrderingService save Order
→ OrderingService publish OrderCreatedIntegrationEvent trực tiếp
→ RabbitMQ
→ NotificationWorker consume event
```

Bài 22 đã làm **consumer-side reliability**.

Bài 23 xử lý phần còn thiếu ở phía tạo event:

```text
Order save DB thành công
→ publish event sang RabbitMQ fail
→ Order đã tồn tại trong DB
→ nhưng event bị mất
→ NotificationWorker không biết order đã được tạo
```

Bài này dùng **Outbox Pattern basic** để xử lý lỗi đó.

Câu nhớ:

```text
Retry/Error Queue/Idempotency xử lý phía consumer.
Outbox xử lý phía publisher.
```

---

## 1. Mục tiêu bài học

Trong 90–120 phút, mục tiêu là:

```text
[ ] Hiểu lỗi "DB saved but publish failed".
[ ] Hiểu Outbox Pattern ở mức basic.
[ ] Tạo bảng OutboxMessages trong database của OrderingService.
[ ] Khi tạo order, lưu Order và OutboxMessage trong cùng transaction.
[ ] Bỏ publish trực tiếp khỏi CreateOrderHandler.
[ ] Tạo Background Publisher đọc OutboxMessages pending.
[ ] Background Publisher publish event sang RabbitMQ bằng MassTransit.
[ ] Publish thành công thì mark OutboxMessage processed.
[ ] Publish fail thì tăng RetryCount và lưu LastError.
[ ] Có debug endpoint để kiểm tra outbox bằng Postman.
[ ] Test được pending/recovery/failure bằng Postman.
```

Bài này chưa làm Transactional Outbox production đầy đủ.

Bản production sâu hơn sẽ học sau:

```text
Buổi 38: Transactional Outbox chuẩn
Buổi 39: Outbox Publisher + Idempotency nâng cao + Inbox/WebhookLog
```

---

## 2. Vấn đề của publish trực tiếp trong handler

Giả sử `CreateOrderHandler` đang làm kiểu:

```csharp
await _orderRepository.CreateAsync(order, cancellationToken);

await _publishEndpoint.Publish(orderCreatedEvent, cancellationToken);
```

Vấn đề là DB và RabbitMQ là hai hệ thống khác nhau.

Không có transaction chung an toàn giữa:

```text
OrderingService database
RabbitMQ broker
```

Vì vậy có thể xảy ra:

```text
Insert Order thành công.
RabbitMQ tạm down.
Publish event fail.
API throw exception.
Order vẫn nằm trong DB.
Event không nằm trong broker.
```

Kết quả:

```text
Data trong database và event flow bị lệch.
```

Đây là lỗi rất phổ biến trong event-driven systems.

Câu nhớ:

```text
Publish trực tiếp trong request handler dễ mất event nếu broker lỗi đúng thời điểm.
```

---

## 3. Outbox Pattern là gì?

Outbox Pattern là cách lưu event cần publish vào chính database của service, cùng transaction với business data.

Thay vì:

```text
Save Order
→ Publish Event trực tiếp
```

Ta đổi thành:

```text
Save Order + Save OutboxMessage cùng DB transaction
→ Background Publisher đọc OutboxMessages pending
→ Publish Event sang RabbitMQ
→ Mark OutboxMessage processed
```

Flow mới:

```text
POST /orders
    |
    v
CreateOrderHandler
    |
    v
Begin DB transaction
    |
    +-- Insert Orders
    |
    +-- Insert OrderItems nếu có
    |
    +-- Insert OutboxMessages
    |
    v
Commit
    |
    v
Return API response

Background Publisher
    |
    v
Read pending OutboxMessages
    |
    v
Publish to RabbitMQ
    |
    v
Mark processed
```

Điểm quan trọng nhất:

```text
Order và OutboxMessage phải được lưu cùng transaction.
```

Nếu commit thành công:

```text
Có Order.
Có OutboxMessage.
Event không mất.
```

Nếu rollback:

```text
Không có Order.
Không có OutboxMessage.
Không publish nhầm event cho order không tồn tại.
```

---

## 4. Outbox basic trong bài này có phạm vi gì?

Bài này làm bản basic phù hợp local learning:

```text
[ ] Bảng OutboxMessages.
[ ] Save Order + OutboxMessage cùng transaction.
[ ] BackgroundService poll pending messages.
[ ] Publish từng message sang RabbitMQ.
[ ] Mark ProcessedAtUtc nếu publish thành công.
[ ] RetryCount + LastError nếu publish fail.
[ ] Debug endpoint để xem trạng thái Outbox bằng Postman.
```

Bài này chưa làm các phần production nâng cao:

```text
[ ] Chưa có locking chuẩn khi scale nhiều instance OrderingService.
[ ] Chưa có SELECT FOR UPDATE SKIP LOCKED.
[ ] Chưa có distributed lock.
[ ] Chưa có exponential backoff.
[ ] Chưa có poison outbox dashboard.
[ ] Chưa có manual replay endpoint/tool.
[ ] Chưa có metrics/alert khi outbox backlog tăng.
[ ] Chưa có event envelope chuẩn.
[ ] Chưa có Inbox table.
```

Câu nhớ:

```text
Outbox basic giúp không mất event.
Outbox production cần thêm locking, backoff, monitoring và replay.
```

---

## 5. Target architecture sau bài 23

Trước bài 23:

```text
POST /orders
→ Save Order
→ Publish OrderCreatedIntegrationEvent trực tiếp
→ RabbitMQ
→ NotificationWorker
```

Sau bài 23:

```text
POST /orders
→ Save Order + OutboxMessage cùng transaction
→ API success

OrderingService Background Publisher
→ đọc OutboxMessages pending
→ publish OrderCreatedIntegrationEvent
→ RabbitMQ
→ NotificationWorker
→ mark OutboxMessage processed
```

Sơ đồ:

```text
Client
  |
  v
OrderingService API
  |
  v
Orders + OutboxMessages
  |
  v
OutboxPublisherBackgroundService
  |
  v
RabbitMQ
  |
  v
NotificationWorker
```

---

## 6. Giả định kỹ thuật của bài

Bài này viết theo giả định project hiện tại đang theo hướng:

```text
OrderingService dùng Dapper.
OrderingService đang có database riêng.
OrderingService đã có OrderRepository.
OrderingService đã dùng MassTransit để publish event.
```

Nếu project của bạn đang dùng SQLite ở Stage 1, dùng SQL SQLite trong bài này.

Nếu project của bạn dùng SQL Server/PostgreSQL, chỉ cần đổi SQL syntax tương ứng.

Tên abstraction connection có thể khác nhau tùy code của bạn:

```text
IDbConnectionFactory
ISqlConnectionFactory
SqliteConnectionFactory
```

Trong bài này dùng tên:

```text
IDbConnectionFactory
```

Nếu code hiện tại dùng tên khác, thay đúng theo project.

---

## 7. Chuẩn bị trước khi làm

Bạn cần có:

```text
[ ] RabbitMQ chạy.
[ ] OrderingService tạo order được.
[ ] OrderingService đang publish OrderCreatedIntegrationEvent.
[ ] BuildingBlocks.Contracts có OrderCreatedIntegrationEvent.
[ ] NotificationWorker consume được event.
[ ] NotificationWorker có retry/error queue/idempotency basic từ bài 22.
```

Chạy:

```bash
docker compose up -d rabbitmq
dotnet build
```

Chạy service:

```bash
dotnet run --project Services/OrderingService/OrderingService.csproj
dotnet run --project Services/NotificationWorker/NotificationWorker.csproj
```

Postman request chính:

```text
POST {{ordering_url}}/orders
```

---

## 8. Scope guard của bài 23

Bài này chỉ sửa `OrderingService`.

Không làm:

```text
[ ] Không sửa NotificationWorker retry/error queue.
[ ] Không tạo Inbox table.
[ ] Không làm Saga.
[ ] Không làm Kafka.
[ ] Không làm MongoDB projection.
[ ] Không làm distributed lock.
[ ] Không làm replay UI.
[ ] Không làm alerting.
```

Lý do:

```text
Buổi 23 chỉ xử lý publisher-side reliability basic.
```

---

## 9. Tạo OutboxMessage model

Tạo folder:

```text
Services/OrderingService/Domain/Outbox
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force Services/OrderingService/Domain/Outbox
```

Tạo file:

```text
Services/OrderingService/Domain/Outbox/OutboxMessage.cs
```

```csharp
namespace OrderingService.Domain.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string Type { get; init; } = default!;
    public string Content { get; init; } = default!;
    public DateTime? ProcessedAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
```

Ý nghĩa:

| Field | Ý nghĩa |
| --- | --- |
| `Id` | Id của outbox message, bài này dùng trùng với EventId |
| `OccurredAtUtc` | Thời điểm event được tạo |
| `Type` | Tên event type |
| `Content` | JSON payload của event |
| `ProcessedAtUtc` | Có giá trị nghĩa là đã publish xong |
| `RetryCount` | Số lần publish fail |
| `LastError` | Lỗi gần nhất khi publish |

Trong bài này:

```text
Type = OrderCreatedIntegrationEvent
Content = JSON của OrderCreatedIntegrationEvent
Id = EventId
```

---

## 10. Tạo bảng OutboxMessages

Nếu dùng SQLite, tạo file:

```text
Services/OrderingService/Infrastructure/Persistence/Scripts/003_CreateOutboxMessages.sql
```

```sql
CREATE TABLE IF NOT EXISTS OutboxMessages (
    Id TEXT NOT NULL PRIMARY KEY,
    OccurredAtUtc TEXT NOT NULL,
    Type TEXT NOT NULL,
    Content TEXT NOT NULL,
    ProcessedAtUtc TEXT NULL,
    RetryCount INTEGER NOT NULL DEFAULT 0,
    LastError TEXT NULL
);

CREATE INDEX IF NOT EXISTS IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc
ON OutboxMessages (ProcessedAtUtc, OccurredAtUtc);
```

Nếu bạn đang có database initializer ở startup, thêm script này vào initializer.

Nếu chưa có initializer, có thể chạy thủ công bằng DB Browser for SQLite hoặc command line.

Ví dụ SQLite CLI:

```bash
sqlite3 ordering.db < Services/OrderingService/Infrastructure/Persistence/Scripts/003_CreateOutboxMessages.sql
```

Nếu dùng SQL Server, schema tương đương:

```sql
CREATE TABLE OutboxMessages (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    OccurredAtUtc DATETIME2 NOT NULL,
    Type NVARCHAR(500) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    ProcessedAtUtc DATETIME2 NULL,
    RetryCount INT NOT NULL DEFAULT 0,
    LastError NVARCHAR(MAX) NULL
);

CREATE INDEX IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc
ON OutboxMessages (ProcessedAtUtc, OccurredAtUtc);
```

Câu nhớ:

```text
OutboxMessages là bảng thuộc database của OrderingService.
Không tạo outbox chung cho toàn bộ hệ thống.
```

---

## 11. Tạo IOutboxRepository

Tạo folder nếu chưa có:

```text
Services/OrderingService/Application/Abstractions
```

Tạo file:

```text
Services/OrderingService/Application/Abstractions/IOutboxRepository.cs
```

```csharp
using System.Data;
using OrderingService.Domain.Outbox;

namespace OrderingService.Application.Abstractions;

public interface IOutboxRepository
{
    Task AddAsync(
        OutboxMessage message,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        int maxRetryCount,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessage>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(
        Guid id,
        DateTime processedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkAsFailedAsync(
        Guid id,
        string error,
        CancellationToken cancellationToken = default);
}
```

Vì sao `AddAsync` nhận `IDbTransaction?`?

```text
Để CreateOrderHandler có thể lưu Order và OutboxMessage trong cùng transaction.
```

Vì sao có `GetLatestAsync`?

```text
Để tạo debug endpoint xem Outbox bằng Postman.
```

---

## 12. Tạo DapperOutboxRepository

Tạo folder:

```text
Services/OrderingService/Infrastructure/Outbox
```

Tạo file:

```text
Services/OrderingService/Infrastructure/Outbox/DapperOutboxRepository.cs
```

```csharp
using System.Data;
using Dapper;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Outbox;

namespace OrderingService.Infrastructure.Outbox;

public sealed class DapperOutboxRepository : IOutboxRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperOutboxRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddAsync(
        OutboxMessage message,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO OutboxMessages (
                Id,
                OccurredAtUtc,
                Type,
                Content,
                ProcessedAtUtc,
                RetryCount,
                LastError
            )
            VALUES (
                @Id,
                @OccurredAtUtc,
                @Type,
                @Content,
                @ProcessedAtUtc,
                @RetryCount,
                @LastError
            );
            """;

        if (transaction is not null)
        {
            await transaction.Connection!.ExecuteAsync(sql, message, transaction);
            return;
        }

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, message);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        int maxRetryCount,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id,
                OccurredAtUtc,
                Type,
                Content,
                ProcessedAtUtc,
                RetryCount,
                LastError
            FROM OutboxMessages
            WHERE ProcessedAtUtc IS NULL
              AND RetryCount < @MaxRetryCount
            ORDER BY OccurredAtUtc
            LIMIT @BatchSize;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var messages = await connection.QueryAsync<OutboxMessage>(
            sql,
            new
            {
                BatchSize = batchSize,
                MaxRetryCount = maxRetryCount
            });

        return messages.AsList();
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id,
                OccurredAtUtc,
                Type,
                Content,
                ProcessedAtUtc,
                RetryCount,
                LastError
            FROM OutboxMessages
            ORDER BY OccurredAtUtc DESC
            LIMIT @Limit;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var messages = await connection.QueryAsync<OutboxMessage>(
            sql,
            new { Limit = limit });

        return messages.AsList();
    }

    public async Task MarkAsProcessedAsync(
        Guid id,
        DateTime processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE OutboxMessages
            SET
                ProcessedAtUtc = @ProcessedAtUtc,
                LastError = NULL
            WHERE Id = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            sql,
            new
            {
                Id = id,
                ProcessedAtUtc = processedAtUtc
            });
    }

    public async Task MarkAsFailedAsync(
        Guid id,
        string error,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE OutboxMessages
            SET
                RetryCount = RetryCount + 1,
                LastError = @LastError
            WHERE Id = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            sql,
            new
            {
                Id = id,
                LastError = error
            });
    }
}
```

Lưu ý:

```text
SQL dùng LIMIT là SQLite syntax.
Nếu dùng SQL Server, đổi LIMIT thành TOP.
Nếu dùng PostgreSQL, LIMIT vẫn dùng được.
```

---

## 13. Register OutboxRepository

Mở:

```text
Services/OrderingService/Program.cs
```

Thêm using:

```csharp
using OrderingService.Application.Abstractions;
using OrderingService.Infrastructure.Outbox;
```

Thêm DI:

```csharp
builder.Services.AddScoped<IOutboxRepository, DapperOutboxRepository>();
```

Vị trí gợi ý:

```csharp
builder.Services.AddScoped<IOrderRepository, DapperOrderRepository>();
builder.Services.AddScoped<IOutboxRepository, DapperOutboxRepository>();
```

Build thử:

```bash
dotnet build Services/OrderingService/OrderingService.csproj
```

---

## 14. Sửa OrderRepository để hỗ trợ transaction

Outbox chỉ đúng khi:

```text
Insert Order
Insert OrderItems nếu có
Insert OutboxMessage
```

được chạy trong cùng transaction.

Nếu `IOrderRepository` hiện tại đang là:

```csharp
Task CreateAsync(Order order, CancellationToken cancellationToken = default);
```

sửa thành:

```csharp
using System.Data;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Abstractions;

public interface IOrderRepository
{
    Task CreateAsync(
        Order order,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<Order?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
```

Trong Dapper repository, mẫu xử lý:

```csharp
public async Task CreateAsync(
    Order order,
    IDbTransaction? transaction = null,
    CancellationToken cancellationToken = default)
{
    const string sql = """
        INSERT INTO Orders (
            Id,
            CustomerId,
            TotalAmount,
            Currency,
            CreatedAtUtc
        )
        VALUES (
            @Id,
            @CustomerId,
            @TotalAmount,
            @Currency,
            @CreatedAtUtc
        );
        """;

    if (transaction is not null)
    {
        await transaction.Connection!.ExecuteAsync(sql, order, transaction);
        return;
    }

    using var connection = _connectionFactory.CreateConnection();
    await connection.ExecuteAsync(sql, order);
}
```

Nếu có `OrderItems`, cũng phải dùng cùng transaction:

```csharp
await transaction.Connection!.ExecuteAsync(orderSql, order, transaction);
await transaction.Connection!.ExecuteAsync(orderItemSql, order.Items, transaction);
```

Không được để OrderItems mở connection riêng.

Câu nhớ:

```text
Nếu Order dùng transaction nhưng OrderItems hoặc Outbox dùng connection khác, Outbox Pattern bị hỏng.
```

---

## 15. Sửa CreateOrderHandler: bỏ publish trực tiếp

Mục tiêu của handler sau bài này:

```text
Create Order object.
Create OrderCreatedIntegrationEvent.
Serialize event thành JSON.
Begin DB transaction.
Save Order.
Save OutboxMessage.
Commit.
Return response.
```

Handler không làm:

```text
Không gọi _publishEndpoint.Publish.
Không inject IPublishEndpoint.
```

Thêm using cần thiết:

```csharp
using System.Text.Json;
using BuildingBlocks.Contracts.Events.Orders;
using OrderingService.Domain.Outbox;
```

---

## 16. Mẫu CreateOrderHandler sau khi dùng Outbox

Mẫu dưới đây để bạn map vào code hiện tại.

Nếu project của bạn đang dùng `Order.Create(...)`, `CreateOrderResponse`, `CreateOrderCommand` khác tên property, giữ style hiện tại và chỉ thay phần publish bằng Outbox.

```csharp
using System.Text.Json;
using BuildingBlocks.Contracts.Events.Orders;
using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Orders;
using OrderingService.Domain.Outbox;

namespace OrderingService.Application.Orders.CreateOrder;

public sealed class CreateOrderHandler
    : IRequestHandler<CreateOrderCommand, CreateOrderResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOrderRepository _orderRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IDbConnectionFactory _connectionFactory;

    public CreateOrderHandler(
        IOrderRepository orderRepository,
        IOutboxRepository outboxRepository,
        IDbConnectionFactory connectionFactory)
    {
        _orderRepository = orderRepository;
        _outboxRepository = outboxRepository;
        _connectionFactory = connectionFactory;
    }

    public async Task<CreateOrderResponse> Handle(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            TotalAmount = request.Items.Sum(x => x.Quantity * x.UnitPrice),
            Currency = "VND",
            CreatedAtUtc = now
        };

        var integrationEvent = new OrderCreatedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = now,
            Version = 1,
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency
        };

        var outboxMessage = new OutboxMessage
        {
            Id = integrationEvent.EventId,
            OccurredAtUtc = integrationEvent.OccurredAtUtc,
            Type = nameof(OrderCreatedIntegrationEvent),
            Content = JsonSerializer.Serialize(integrationEvent, JsonOptions),
            ProcessedAtUtc = null,
            RetryCount = 0,
            LastError = null
        };

        using var connection = _connectionFactory.CreateConnection();

        // Nếu connection factory của bạn đã open sẵn connection thì bỏ dòng này.
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            await _orderRepository.CreateAsync(
                order,
                transaction,
                cancellationToken);

            await _outboxRepository.AddAsync(
                outboxMessage,
                transaction,
                cancellationToken);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return new CreateOrderResponse
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            CreatedAtUtc = order.CreatedAtUtc
        };
    }
}
```

Điểm cần check kỹ:

```text
[ ] Handler không còn IPublishEndpoint.
[ ] Handler không còn await Publish(...).
[ ] Order và OutboxMessage dùng cùng transaction.
[ ] OutboxMessage.Id = integrationEvent.EventId.
[ ] OutboxMessage.Type = nameof(OrderCreatedIntegrationEvent).
```

---

## 17. Vì sao OutboxMessage.Id dùng EventId?

Bài này set:

```csharp
Id = integrationEvent.EventId
```

Lợi ích:

```text
Dễ trace từ DB sang log consumer.
Duplicate publish vẫn dùng cùng EventId.
NotificationWorker bài 22 có thể skip duplicate theo EventId.
```

Câu nhớ:

```text
EventId là identity của event.
OrderId là identity của order.
Hai cái không nên trộn lẫn.
```

---

## 18. Tạo OutboxPublisherOptions

Tạo file:

```text
Services/OrderingService/Infrastructure/Outbox/OutboxPublisherOptions.cs
```

```csharp
namespace OrderingService.Infrastructure.Outbox;

public sealed class OutboxPublisherOptions
{
    public bool Enabled { get; init; } = true;
    public int BatchSize { get; init; } = 10;
    public int IntervalSeconds { get; init; } = 5;
    public int MaxRetryCount { get; init; } = 10;
    public bool SimulatePublishFailure { get; init; } = false;
}
```

Thêm vào:

```text
Services/OrderingService/appsettings.Development.json
```

```json
{
  "OutboxPublisher": {
    "Enabled": true,
    "BatchSize": 10,
    "IntervalSeconds": 5,
    "MaxRetryCount": 10,
    "SimulatePublishFailure": false
  }
}
```

Ý nghĩa:

| Option | Ý nghĩa |
| --- | --- |
| `Enabled` | Bật/tắt background publisher |
| `BatchSize` | Mỗi vòng lấy tối đa bao nhiêu message |
| `IntervalSeconds` | Bao lâu poll DB một lần |
| `MaxRetryCount` | Không publish lại vô hạn message lỗi |
| `SimulatePublishFailure` | Giả lập lỗi publish để test |

---

## 19. Tạo OutboxPublisherBackgroundService

Tạo file:

```text
Services/OrderingService/Infrastructure/Outbox/OutboxPublisherBackgroundService.cs
```

```csharp
using System.Text.Json;
using BuildingBlocks.Contracts.Events.Orders;
using MassTransit;
using Microsoft.Extensions.Options;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Outbox;

namespace OrderingService.Infrastructure.Outbox;

public sealed class OutboxPublisherBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;
    private readonly OutboxPublisherOptions _options;

    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherBackgroundService> logger,
        IOptions<OutboxPublisherOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Outbox publisher is disabled.");
            return;
        }

        _logger.LogInformation(
            "Outbox publisher started. BatchSize={BatchSize}, IntervalSeconds={IntervalSeconds}, MaxRetryCount={MaxRetryCount}",
            _options.BatchSize,
            _options.IntervalSeconds,
            _options.MaxRetryCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            await PublishPendingMessagesSafelyAsync(stoppingToken);

            await Task.Delay(
                TimeSpan.FromSeconds(_options.IntervalSeconds),
                stoppingToken);
        }
    }

    private async Task PublishPendingMessagesSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PublishPendingMessagesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // App is shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while publishing outbox messages.");
        }
    }

    private async Task PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await outboxRepository.GetPendingAsync(
            _options.BatchSize,
            _options.MaxRetryCount,
            cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Found {Count} pending outbox message(s).",
            messages.Count);

        foreach (var message in messages)
        {
            await PublishSingleMessageAsync(
                outboxRepository,
                publishEndpoint,
                message,
                cancellationToken);
        }
    }

    private async Task PublishSingleMessageAsync(
        IOutboxRepository outboxRepository,
        IPublishEndpoint publishEndpoint,
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_options.SimulatePublishFailure)
            {
                throw new InvalidOperationException("Simulated outbox publish failure.");
            }

            await PublishMessageAsync(
                publishEndpoint,
                message.Type,
                message.Content,
                cancellationToken);

            await outboxRepository.MarkAsProcessedAsync(
                message.Id,
                DateTime.UtcNow,
                cancellationToken);

            _logger.LogInformation(
                "Outbox message published. OutboxMessageId={OutboxMessageId}, Type={Type}",
                message.Id,
                message.Type);
        }
        catch (Exception ex)
        {
            await outboxRepository.MarkAsFailedAsync(
                message.Id,
                ex.Message,
                cancellationToken);

            _logger.LogError(
                ex,
                "Failed to publish outbox message. OutboxMessageId={OutboxMessageId}, Type={Type}",
                message.Id,
                message.Type);
        }
    }

    private static async Task PublishMessageAsync(
        IPublishEndpoint publishEndpoint,
        string type,
        string content,
        CancellationToken cancellationToken)
    {
        if (type == nameof(OrderCreatedIntegrationEvent))
        {
            var integrationEvent = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(
                content,
                JsonOptions);

            if (integrationEvent is null)
            {
                throw new InvalidOperationException(
                    $"Cannot deserialize outbox message content to {nameof(OrderCreatedIntegrationEvent)}.");
            }

            await publishEndpoint.Publish(integrationEvent, cancellationToken);
            return;
        }

        throw new NotSupportedException($"Unsupported outbox message type: {type}");
    }
}
```

Vì sao dùng `IServiceScopeFactory`?

```text
BackgroundService là singleton.
Repository và DbConnection thường là scoped.
Vì vậy mỗi vòng xử lý cần tạo scope riêng.
```

Điểm cần hiểu:

```text
Publisher publish thành công rồi mới mark ProcessedAtUtc.
Nếu publish thành công nhưng mark processed fail, message có thể publish lại.
Consumer idempotency bài 22 giúp giảm tác hại duplicate.
```

---

## 20. Register Outbox Publisher

Mở:

```text
Services/OrderingService/Program.cs
```

Thêm:

```csharp
builder.Services.Configure<OutboxPublisherOptions>(
    builder.Configuration.GetSection("OutboxPublisher"));

builder.Services.AddScoped<IOutboxRepository, DapperOutboxRepository>();

builder.Services.AddHostedService<OutboxPublisherBackgroundService>();
```

Vị trí gợi ý:

```csharp
builder.Services.Configure<OutboxPublisherOptions>(
    builder.Configuration.GetSection("OutboxPublisher"));

builder.Services.AddScoped<IOrderRepository, DapperOrderRepository>();
builder.Services.AddScoped<IOutboxRepository, DapperOutboxRepository>();

builder.Services.AddHostedService<OutboxPublisherBackgroundService>();
```

Build:

```bash
dotnet build Services/OrderingService/OrderingService.csproj
```

---

## 21. Thêm debug endpoint cho Outbox

Vì khóa học này Postman-first, ta thêm endpoint debug để xem OutboxMessages mà không cần mở DB tool.

Trong `Program.cs`, sau khi build app:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapGet("/debug/outbox", async (
        IOutboxRepository outboxRepository,
        int limit,
        CancellationToken cancellationToken) =>
    {
        if (limit <= 0 || limit > 100)
        {
            limit = 20;
        }

        var messages = await outboxRepository.GetLatestAsync(
            limit,
            cancellationToken);

        var result = messages.Select(x => new
        {
            x.Id,
            x.Type,
            x.OccurredAtUtc,
            x.ProcessedAtUtc,
            x.RetryCount,
            x.LastError,
            Status = x.ProcessedAtUtc is null ? "Pending" : "Processed"
        });

        return Results.Ok(result);
    })
    .WithTags("Debug")
    .WithName("DebugOutbox");
}
```

Test bằng Postman:

```text
GET {{ordering_url}}/debug/outbox?limit=20
```

Expected response mẫu:

```json
[
  {
    "id": "99999999-9999-9999-9999-999999999999",
    "type": "OrderCreatedIntegrationEvent",
    "occurredAtUtc": "2026-05-25T15:00:00Z",
    "processedAtUtc": "2026-05-25T15:00:05Z",
    "retryCount": 0,
    "lastError": null,
    "status": "Processed"
  }
]
```

Lưu ý:

```text
Endpoint này chỉ để Development.
Không đưa debug endpoint kiểu này ra production.
```

---

## 22. Checklist trước khi test

Trước khi test, check:

```text
[ ] OutboxMessages table đã tồn tại.
[ ] IOutboxRepository đã register.
[ ] OutboxPublisherBackgroundService đã register.
[ ] CreateOrderHandler không còn IPublishEndpoint.
[ ] CreateOrderHandler không còn Publish trực tiếp.
[ ] OrderRepository dùng được transaction.
[ ] appsettings.Development.json có OutboxPublisher config.
[ ] DOTNET_ENVIRONMENT=Development nếu cần load config.
```

Chạy:

```powershell
$env:DOTNET_ENVIRONMENT="Development"
dotnet run --project Services/OrderingService/OrderingService.csproj
```

Terminal khác:

```powershell
$env:DOTNET_ENVIRONMENT="Development"
dotnet run --project Services/NotificationWorker/NotificationWorker.csproj
```

RabbitMQ:

```bash
docker compose up -d rabbitmq
```

---

## 23. Postman Lab

Collection:

```text
MicroShop - Lesson 23 Outbox Basic
```

Environment:

| Variable | Value |
| --- | --- |
| `ordering_url` | `http://localhost:5004` |
| `rabbitmq_ui` | `http://localhost:15672` |

---

### Request 1: Create order normal

```text
POST {{ordering_url}}/orders
```

Body mẫu:

```json
{
  "customerId": "11111111-1111-1111-1111-111111111111",
  "items": [
    {
      "productId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "productName": "MacBook Pro",
      "quantity": 1,
      "unitPrice": 1977.3
    }
  ]
}
```

Expected:

```text
API success.
Order được lưu DB.
OutboxMessage được tạo.
Background Publisher publish event.
OutboxMessage được mark ProcessedAtUtc.
NotificationWorker consume event.
```

Check Outbox:

```text
GET {{ordering_url}}/debug/outbox?limit=20
```

Expected:

```text
Status = Processed
RetryCount = 0
LastError = null
```

---

### Request 2: Tắt OutboxPublisher để test pending

Set config:

```json
"OutboxPublisher": {
  "Enabled": false,
  "BatchSize": 10,
  "IntervalSeconds": 5,
  "MaxRetryCount": 10,
  "SimulatePublishFailure": false
}
```

Restart OrderingService.

Gửi:

```text
POST {{ordering_url}}/orders
```

Check:

```text
GET {{ordering_url}}/debug/outbox?limit=20
```

Expected:

```text
API vẫn success.
OutboxMessage được lưu DB.
Status = Pending.
ProcessedAtUtc = null.
NotificationWorker chưa nhận event mới.
```

Ý nghĩa:

```text
Event chưa publish nhưng không mất.
Nó đang nằm trong OutboxMessages.
```

---

### Request 3: Bật lại OutboxPublisher để test recovery

Set:

```json
"Enabled": true
```

Restart OrderingService.

Đợi vài giây rồi gọi:

```text
GET {{ordering_url}}/debug/outbox?limit=20
```

Expected:

```text
Pending message được publish.
Status chuyển thành Processed.
ProcessedAtUtc có giá trị.
NotificationWorker nhận event.
```

Câu nhớ:

```text
Outbox giúp event còn đó để publish sau.
```

---

### Request 4: Simulate publish failure

Set:

```json
"OutboxPublisher": {
  "Enabled": true,
  "BatchSize": 10,
  "IntervalSeconds": 5,
  "MaxRetryCount": 10,
  "SimulatePublishFailure": true
}
```

Restart OrderingService.

Gửi:

```text
POST {{ordering_url}}/orders
```

Check nhiều lần:

```text
GET {{ordering_url}}/debug/outbox?limit=20
```

Expected:

```text
API vẫn success.
OutboxMessage được tạo.
Status = Pending.
RetryCount tăng dần.
LastError = Simulated outbox publish failure.
ProcessedAtUtc = null.
```

Sau đó set:

```json
"SimulatePublishFailure": false
```

Restart OrderingService.

Expected:

```text
Publisher publish lại pending message.
Status chuyển thành Processed.
NotificationWorker nhận event.
```

---

### Request 5: Tắt NotificationWorker

Test này để phân biệt rõ Outbox và Consumer reliability.

Setup:

```text
RabbitMQ chạy.
OrderingService chạy.
OutboxPublisher Enabled=true.
NotificationWorker tắt.
```

Gửi:

```text
POST {{ordering_url}}/orders
```

Expected:

```text
API success.
OutboxMessage status = Processed.
RabbitMQ queue notification-order-created có message chờ.
NotificationWorker chưa log gì vì đang tắt.
```

Bật lại NotificationWorker.

Expected:

```text
NotificationWorker consume message.
Nếu consumer lỗi, retry/error queue của bài 22 xử lý.
```

Câu nhớ:

```text
Outbox đảm bảo event được publish ra broker.
Retry/error queue/idempotency đảm bảo xử lý phía consumer tốt hơn.
Hai phần bổ sung nhau.
```

---

## 24. Kiểm tra RabbitMQ UI

Mở:

```text
http://localhost:15672
```

Login:

```text
microshop / microshop
```

Vào:

```text
Queues and Streams
```

Quan sát:

```text
notification-order-created
notification-order-created_error
```

Kịch bản:

```text
NotificationWorker đang chạy:
    Queue chính thường về 0 nhanh.

NotificationWorker tắt:
    Queue chính tăng message count.

NotificationWorker lỗi:
    Message retry rồi có thể vào notification-order-created_error.
```

---

## 25. Lỗi hay gặp

| Lỗi | Nguyên nhân | Cách xử lý |
| --- | --- | --- |
| `no such table: OutboxMessages` | Chưa chạy script tạo bảng | Chạy script hoặc thêm vào initializer |
| API tạo order lỗi sau khi thêm transaction | Connection factory chưa open/transaction dùng sai connection | Kiểm tra `connection.Open()` và repository dùng `transaction.Connection` |
| Có Order nhưng không có OutboxMessage | Chưa gọi AddAsync trong transaction hoặc transaction rollback sau insert Order | Kiểm tra handler và catch/rollback |
| Có OutboxMessage nhưng không publish | Publisher disabled, chưa register HostedService, RabbitMQ down | Kiểm tra config, logs, RabbitMQ |
| `Unsupported outbox message type` | Type lưu trong DB không match `nameof(OrderCreatedIntegrationEvent)` | Kiểm tra `outboxMessage.Type` |
| Deserialize null/lỗi JSON | Content sai schema hoặc serializer options không khớp | Check JSON trong OutboxMessages |
| RetryCount tăng liên tục | `SimulatePublishFailure=true` hoặc RabbitMQ lỗi | Tắt simulate failure, kiểm tra broker |
| Duplicate notification | Publish thành công nhưng mark processed fail, hoặc duplicate event | Consumer idempotency bài 22 cần hoạt động |
| Pending message không được pick nữa | RetryCount đã đạt MaxRetryCount | Tăng MaxRetryCount hoặc sửa lỗi rồi reset RetryCount thủ công khi học |
| Build fail vì `IDbConnectionFactory` không tồn tại | Project đặt tên abstraction khác | Thay bằng tên factory hiện tại của project |

---

## 26. Bài tập

### Bài 1: Vẽ flow trước và sau Outbox

Vẽ:

```text
Before:
POST /orders
→ Save Order
→ Publish Event trực tiếp
→ RabbitMQ

After:
POST /orders
→ Save Order + OutboxMessage
→ Background Publisher
→ RabbitMQ
```

Trả lời:

```text
Outbox giải quyết lỗi gì?
Vì sao event không bị mất nếu RabbitMQ tạm down?
```

---

### Bài 2: Test pending outbox

Set:

```json
"Enabled": false
```

Gửi 1 order.

Gọi:

```text
GET {{ordering_url}}/debug/outbox?limit=20
```

Ghi lại:

```text
OutboxMessage Id là gì?
Type là gì?
Status là gì?
ProcessedAtUtc là null hay có giá trị?
NotificationWorker có nhận event không?
```

---

### Bài 3: Test recovery

Bật lại publisher.

Gọi lại:

```text
GET {{ordering_url}}/debug/outbox?limit=20
```

Ghi lại:

```text
Sau bao lâu status chuyển thành Processed?
NotificationWorker có nhận event không?
```

---

### Bài 4: Test publish failure

Set:

```json
"SimulatePublishFailure": true
```

Gửi 1 order.

Gọi debug outbox vài lần.

Ghi lại:

```text
RetryCount tăng thế nào?
LastError lưu gì?
ProcessedAtUtc có null không?
```

Sau đó set lại:

```json
"SimulatePublishFailure": false
```

Ghi lại:

```text
Message có được publish lại không?
```

---

### Bài 5: Viết production note

Trả lời:

```text
Vì sao Outbox basic bài này chưa đủ production?
Production cần bổ sung gì?
```

Gợi ý:

```text
Multi-instance locking.
Backoff policy.
Max retry/poison handling.
Metrics/alerting.
Manual replay tool.
Event envelope/versioning.
```

---

## 27. Quiz nhanh

**Câu 1. Outbox Pattern giải quyết vấn đề chính nào?**

```text
A. Consumer bị duplicate message
B. DB save thành công nhưng publish event fail
C. Gateway route sai
D. JWT hết hạn
```

Đáp án: B

**Câu 2. Order và OutboxMessage nên được lưu thế nào?**

```text
A. Hai database khác nhau, không cần transaction
B. Cùng transaction trong database của OrderingService
C. Lưu OutboxMessage trong RabbitMQ
D. Lưu Order trong Redis
```

Đáp án: B

**Câu 3. Sau bài 23, CreateOrderHandler còn publish event trực tiếp không?**

```text
A. Có, publish trực tiếp như cũ
B. Không, handler chỉ lưu OutboxMessage
C. Có, nhưng publish sang Kafka
D. Không, bỏ event hoàn toàn
```

Đáp án: B

**Câu 4. Background Publisher làm gì?**

```text
A. Đọc OutboxMessages pending và publish sang RabbitMQ
B. Validate JWT
C. Tạo database schema
D. Consume NotificationWorker
```

Đáp án: A

**Câu 5. Nếu OutboxPublisher tắt, tạo order có mất event không?**

```text
A. Có, event mất luôn
B. Không, event vẫn nằm trong OutboxMessages với ProcessedAtUtc = null
C. Không tạo được order
D. RabbitMQ tự lưu event
```

Đáp án: B

---

## 28. Production mindset

Sau bài 23, MicroShop tốt hơn ở điểm:

```text
OrderCreatedIntegrationEvent không còn phụ thuộc vào việc RabbitMQ publish thành công ngay trong HTTP request.
```

Đã có:

```text
OutboxMessages table.
Save Order + OutboxMessage cùng transaction.
Background Publisher đọc pending messages.
Publish sang RabbitMQ.
Mark processed.
Mark failed + RetryCount + LastError.
Debug endpoint để kiểm tra bằng Postman.
```

Chưa có:

```text
Multi-instance safe locking.
Backoff theo thời gian.
Poison outbox handling chuẩn.
Manual replay tool.
Metrics cho pending outbox count.
Alert khi outbox backlog tăng.
Event envelope chuẩn.
CorrelationId đầy đủ.
Inbox table phía consumer.
```

Câu nhớ:

```text
Outbox không làm RabbitMQ hết lỗi.
Outbox đảm bảo event không bị mất khi broker lỗi tạm thời.
```

---

## 29. Điều kiện pass bài trong 90–120 phút

Bạn pass Buổi 23 khi:

```text
[ ] Có bảng OutboxMessages.
[ ] CreateOrderHandler không publish event trực tiếp nữa.
[ ] CreateOrderHandler save Order + OutboxMessage cùng transaction.
[ ] OutboxPublisherBackgroundService chạy được.
[ ] Publisher đọc pending outbox message.
[ ] Publisher publish OrderCreatedIntegrationEvent sang RabbitMQ.
[ ] Publish thành công thì ProcessedAtUtc có giá trị.
[ ] Publish fail thì RetryCount tăng và LastError có dữ liệu.
[ ] Debug endpoint `/debug/outbox` xem được trạng thái bằng Postman.
[ ] Postman tạo order vẫn success.
[ ] NotificationWorker nhận event từ RabbitMQ.
[ ] Bạn giải thích được Outbox giải quyết lỗi gì.
[ ] Bạn phân biệt được bài 22 consumer-side reliability và bài 23 publisher-side reliability.
```

Nếu hôm nay chỉ làm được flow này là đạt mục tiêu chính:

```text
POST /orders
→ Orders + OutboxMessages
→ Background Publisher
→ RabbitMQ
→ NotificationWorker
```

---

## 30. Không làm trong bài này

Không làm:

```text
[ ] Không làm Inbox table.
[ ] Không làm Saga.
[ ] Không làm Kafka.
[ ] Không làm MongoDB projection.
[ ] Không làm distributed lock.
[ ] Không làm retry/backoff nâng cao cho publisher.
[ ] Không làm dashboard.
[ ] Không làm alerting.
[ ] Không làm replay UI.
```

Lý do:

```text
Bài 23 chỉ cần hiểu và build Outbox basic.
```

---

## 31. Điều kiện mở khóa Buổi 24

Bạn có thể sang Buổi 24 khi:

```text
[ ] Event tạo order không publish trực tiếp trong handler nữa.
[ ] Event được lưu vào OutboxMessages.
[ ] Background Publisher publish được event sang RabbitMQ.
[ ] NotificationWorker consume được event đó.
[ ] Bạn hiểu Outbox khác retry/error queue.
```

Buổi 24 sẽ học:

```text
RabbitMQ vs Kafka
```

Mục tiêu Buổi 24:

```text
Hiểu khi nào dùng RabbitMQ.
Hiểu khi nào dùng Kafka.
Không thay RabbitMQ bằng Kafka bừa bãi.
Có decision matrix cho MicroShop.
```

---

## 32. Checkpoint nhỏ cuối Phase 1.5

Sau bài 23, phần RabbitMQ reliability basic có hình như sau:

```text
OrderingService
  |
  +-- Orders
  |
  +-- OutboxMessages
  |
  +-- OutboxPublisherBackgroundService
          |
          v
        RabbitMQ
          |
          v
NotificationWorker
  |
  +-- Retry
  |
  +-- Error Queue
  |
  +-- Idempotency Basic
```

Bạn cần nói được:

```text
1. Vì sao publish trực tiếp trong handler nguy hiểm?
2. Outbox lưu event ở đâu?
3. Vì sao Order và OutboxMessage phải cùng transaction?
4. Background Publisher làm gì?
5. Nếu RabbitMQ down thì event có mất không?
6. Nếu consumer lỗi thì Outbox có xử lý không?
7. Bài 22 và bài 23 khác nhau thế nào?
```

Câu trả lời chuẩn:

```text
Bài 22 làm consumer đáng tin hơn.
Bài 23 làm publisher đáng tin hơn.
Cả hai kết hợp mới tạo được event-driven flow ổn hơn.
```
