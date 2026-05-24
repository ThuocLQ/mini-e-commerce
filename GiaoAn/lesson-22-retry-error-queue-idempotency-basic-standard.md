---
lesson: 22
title: "Retry / Error Queue + Idempotency Basic"
duration: "90–120 phút"
phase: "Phase 1.5 - RabbitMQ + Reliability Basic"
project: "MicroShop"
testing: "Postman-first"
type: "lesson"
roadmap_alignment: "Buổi 22 đúng roadmap: Retry/DLQ + Idempotency Basic. Buổi 23 mới là Outbox Basic + Background Publisher Intro."
---

# Buổi 22: Retry / Error Queue + Idempotency Basic

## 0. Vị trí trong lộ trình

Theo lộ trình chuẩn Phase 1.5:

```text
Buổi 19: RabbitMQ + MassTransit
Buổi 20: BuildingBlocks.Contracts + Event Design
Buổi 21: NotificationWorker
Buổi 22: Retry/DLQ + Idempotency Basic
Buổi 23: Outbox Basic + Background Publisher Intro
```

Bạn đã làm xong:

```text
Buổi 19:
OrderingService publish event bằng MassTransit.

Buổi 20:
Tạo BuildingBlocks.Contracts.
Tạo OrderCreatedIntegrationEvent.

Buổi 21:
Tạo NotificationWorker.
NotificationWorker consume OrderCreatedIntegrationEvent.
```

Hiện tại flow đã chạy được:

```text
POST /orders
→ OrderingService publish OrderCreatedIntegrationEvent
→ RabbitMQ
→ NotificationWorker consume event
→ log notification
```

Nhưng flow này vẫn chưa đủ tin cậy.

Nếu consumer lỗi thì sao?

```text
Consumer throw exception
→ message được retry
→ retry hết vẫn lỗi
→ message được đưa vào error queue
```

Nếu cùng một event bị xử lý lại thì sao?

```text
Cùng EventId được deliver lại
→ NotificationWorker có thể gửi notification trùng
→ cần idempotency basic
```

Câu nhớ:

```text
Consumer chạy được là bước đầu.
Consumer đáng tin cần retry, error queue và idempotency.
```

---

## 1. Mục tiêu bài học

Trong 90–120 phút, mục tiêu là:

```text
[ ] Hiểu retry trong message consumer là gì.
[ ] Hiểu MassTransit error queue là gì và vì sao nó giống DLQ ở mức bài học.
[ ] Config retry policy cho NotificationWorker bằng MassTransit.
[ ] Cố tình làm consumer lỗi để quan sát retry.
[ ] Quan sát error queue trong RabbitMQ UI.
[ ] Hiểu idempotency là gì trong consumer.
[ ] Tạo idempotency basic bằng in-memory store theo EventId.
[ ] Test duplicate event không xử lý notification 2 lần.
[ ] Hiểu giới hạn của in-memory idempotency.
```

Bài này xử lý **consumer-side reliability**.

Bài này không sửa flow publish của OrderingService. OrderingService vẫn publish trực tiếp như bài 19–21. Outbox để bài 23 mới xử lý **publisher-side reliability**.

---

## 2. Vấn đề sau Buổi 21

Buổi 21 consumer đang xử lý rất đơn giản:

```csharp
public Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
{
    _logger.LogInformation("Received event...");
    return Task.CompletedTask;
}
```

Nếu xử lý thành công:

```text
MassTransit ack message.
RabbitMQ xóa message khỏi queue.
```

Nhưng nếu xử lý lỗi:

```text
Consumer throw exception.
Message chưa được xử lý thành công.
Cần quyết định retry hay đưa vào error queue.
```

Ví dụ lỗi thực tế:

```text
SMTP provider timeout.
Notification service dependency down.
Database log insert fail.
External API trả 500.
Bug trong consumer code.
```

Nếu không có retry/error queue/idempotency:

```text
Lỗi tạm thời có thể làm mất cơ hội xử lý lại.
Lỗi poison message có thể làm consumer fail liên tục.
Duplicate delivery có thể gây gửi notification trùng.
```

---

## 3. Retry là gì?

Retry nghĩa là khi consumer xử lý message lỗi, hệ thống thử xử lý lại message đó.

Ví dụ:

```text
Lần xử lý ban đầu: gọi email provider timeout
→ retry 1 sau 2 giây
→ retry 2 sau 2 giây
→ retry 3 sau 2 giây
→ nếu vẫn lỗi thì đưa vào error queue
```

Retry phù hợp với lỗi tạm thời:

```text
Network timeout.
Broker transient error.
External service trả 503.
Database connection lỗi ngắn hạn.
```

Retry không phù hợp với lỗi vĩnh viễn:

```text
Payload sai schema.
OrderId rỗng.
Business rule không hợp lệ.
Bug code consumer.
```

Câu nhớ:

```text
Retry giúp vượt qua lỗi tạm thời.
Retry không sửa được dữ liệu sai hoặc bug code.
```

---

## 4. Error Queue và DLQ khác nhau thế nào?

Trong nhiều hệ thống, người ta gọi chung là DLQ:

```text
Dead Letter Queue
```

Ý tưởng chung:

```text
Message xử lý thất bại nhiều lần
→ không retry vô hạn
→ đưa vào một queue lỗi để debug/replay/manual handling
```

Trong bài này dùng MassTransit với RabbitMQ. Khi consumer retry hết nhưng vẫn lỗi, MassTransit thường đưa message vào **error queue**.

Ví dụ receive endpoint tên:

```text
notification-order-created
```

Error queue thường là:

```text
notification-order-created_error
```

Điểm cần phân biệt:

```text
MassTransit *_error queue = error queue do MassTransit quản lý.
RabbitMQ native DLX/DLQ = cơ chế dead-letter-exchange/dead-letter-queue của RabbitMQ.
```

Trong bài này, ta gọi `notification-order-created_error` là **error queue / DLQ-like queue** để dễ hiểu. RabbitMQ native DLX/DLQ với `x-dead-letter-exchange` là phần nâng cao hơn, chưa cần triển khai hôm nay.

Câu nhớ:

```text
Retry xử lý lỗi tạm thời.
Error queue giữ lại message không xử lý được sau retry.
```

---

## 5. Retry count cần hiểu chính xác

Trong MassTransit, config:

```csharp
r.Interval(3, TimeSpan.FromSeconds(2));
```

Nên hiểu là:

```text
1 lần xử lý ban đầu.
Nếu lỗi, retry thêm 3 lần.
Tổng số lần method Consume có thể được gọi = 4 lần.
```

Vì vậy khi xem log, bạn có thể thấy consumer log lỗi nhiều hơn con số “3”.

Câu nhớ:

```text
Interval(3, ...) = 3 retries, không phải tổng 3 attempts.
```

---

## 6. Idempotency là gì?

Idempotency nghĩa là xử lý cùng một input nhiều lần nhưng kết quả side effect không bị lặp sai.

Trong consumer:

```text
Cùng EventId được deliver 2 lần
→ consumer chỉ gửi notification 1 lần
→ lần sau detect processed và skip
```

Vì sao message có thể duplicate?

```text
Consumer xử lý xong side effect nhưng crash trước khi ack.
Broker redeliver message.
Publisher gửi trùng.
Retry làm code side effect chạy lại nếu đặt sai vị trí.
```

Ví dụ nguy hiểm:

```text
Gửi email thành công
→ app crash trước ack
→ message được deliver lại
→ gửi email lần 2
```

Câu nhớ:

```text
Trong distributed systems, hãy giả định message có thể được xử lý hơn một lần.
```

---

## 7. Bài này làm idempotency basic như thế nào?

Bài này làm bản đơn giản:

```text
In-memory store lưu EventId đã xử lý.
Consumer nhận event.
Nếu EventId đã xử lý → skip.
Nếu chưa xử lý → xử lý notification → mark processed.
```

Flow:

```text
Receive event
→ Check EventId in store
→ Nếu đã có: log skip
→ Nếu chưa có: simulate notification
→ Mark EventId as processed
```

Giới hạn quan trọng:

```text
In-memory mất dữ liệu khi worker restart.
Không share giữa nhiều instance worker.
Chưa chống duplicate tuyệt đối nếu 2 message cùng EventId được xử lý song song.
```

Vì sao có race condition?

```text
Message A cùng EventId vào consumer 1.
Message B cùng EventId vào consumer 2.
Cả hai cùng check HasProcessedAsync = false.
Cả hai đều xử lý side effect.
Sau đó cả hai mới MarkAsProcessedAsync.
```

Production hơn sẽ dùng:

```text
Database Inbox / ProcessedMessages table.
Unique constraint theo EventId.
Atomic insert/check.
Transaction rõ ràng.
```

Phần này học sâu hơn sau. Hôm nay chỉ cần hiểu concept.

---

## 8. Target architecture sau bài này

Trước bài 22:

```text
RabbitMQ
→ NotificationWorker Consumer
→ Log notification
```

Sau bài 22:

```text
RabbitMQ
→ NotificationWorker Consumer
   → Retry policy
   → Error queue nếu fail sau retry
   → Idempotency check theo EventId
   → Log notification nếu chưa xử lý
```

Sơ đồ:

```text
OrderCreatedIntegrationEvent
    |
    v
notification-order-created queue
    |
    v
Consumer
    |
    +-- check EventId processed?
    |       |
    |       +-- yes → skip
    |       |
    |       +-- no → process notification
    |
    +-- if exception → retry
            |
            +-- still fail → notification-order-created_error
```

---

## 9. Chuẩn bị trước khi làm

Bạn cần có từ bài 21:

```text
[ ] RabbitMQ chạy.
[ ] OrderingService publish OrderCreatedIntegrationEvent.
[ ] NotificationWorker consume được event.
[ ] Queue notification-order-created tồn tại.
[ ] Postman tạo order và worker log event.
```

Chạy trước:

```bash
docker compose up -d rabbitmq
dotnet build
```

Mở RabbitMQ UI:

```text
http://localhost:15672
```

Login:

```text
microshop / microshop
```

---

## 10. Scope guard của bài 22

Bài 22 chỉ sửa NotificationWorker.

Không sửa những phần sau:

```text
[ ] Không sửa CreateOrderHandler để làm Outbox.
[ ] Không tạo Outbox table.
[ ] Không tạo Background Publisher.
[ ] Không thay đổi event contract.
[ ] Không tạo thêm Inventory/Shipping.
```

Lý do:

```text
Retry/error queue/idempotency là consumer-side reliability.
Outbox là publisher-side reliability và để Buổi 23.
```

---

## 11. Tạo abstraction idempotency store

Trong NotificationWorker, tạo folder:

```text
Services/NotificationWorker/Application/Abstractions
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force Services/NotificationWorker/Application/Abstractions
```

Tạo file:

```text
Services/NotificationWorker/Application/Abstractions/IProcessedEventStore.cs
```

```csharp
namespace NotificationWorker.Application.Abstractions;

public interface IProcessedEventStore
{
    Task<bool> HasProcessedAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);
}
```

Ý nghĩa:

```text
HasProcessedAsync: kiểm tra EventId đã xử lý chưa.
MarkAsProcessedAsync: đánh dấu EventId đã xử lý.
```

Bài này dùng in-memory implementation.

---

## 12. Tạo in-memory implementation

Tạo folder:

```text
Services/NotificationWorker/Infrastructure/Idempotency
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force Services/NotificationWorker/Infrastructure/Idempotency
```

Tạo file:

```text
Services/NotificationWorker/Infrastructure/Idempotency/InMemoryProcessedEventStore.cs
```

```csharp
using System.Collections.Concurrent;
using NotificationWorker.Application.Abstractions;

namespace NotificationWorker.Infrastructure.Idempotency;

public sealed class InMemoryProcessedEventStore : IProcessedEventStore
{
    private readonly ConcurrentDictionary<Guid, byte> _processedEvents = new();

    public Task<bool> HasProcessedAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var hasProcessed = _processedEvents.ContainsKey(eventId);
        return Task.FromResult(hasProcessed);
    }

    public Task MarkAsProcessedAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        _processedEvents.TryAdd(eventId, 0);
        return Task.CompletedTask;
    }
}
```

Vì sao dùng `ConcurrentDictionary`?

```text
Worker có thể xử lý message song song.
ConcurrentDictionary an toàn hơn Dictionary trong multi-thread scenario.
```

Nhưng nhớ:

```text
ConcurrentDictionary chỉ giúp collection thread-safe.
Nó chưa biến flow HasProcessed → Process → MarkAsProcessed thành atomic transaction.
```

---

## 13. Register idempotency store trong Program.cs

Mở:

```text
Services/NotificationWorker/Program.cs
```

Thêm using:

```csharp
using NotificationWorker.Application.Abstractions;
using NotificationWorker.Infrastructure.Idempotency;
```

Thêm DI registration:

```csharp
builder.Services.AddSingleton<IProcessedEventStore, InMemoryProcessedEventStore>();
```

Vị trí gợi ý:

```csharp
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddSingleton<IProcessedEventStore, InMemoryProcessedEventStore>();

builder.Services.AddMassTransit(x =>
{
    // ...
});
```

Build thử:

```bash
dotnet build Services/NotificationWorker/NotificationWorker.csproj
```

---

## 14. Thêm idempotency vào consumer

Mở file:

```text
Services/NotificationWorker/Consumers/OrderCreatedIntegrationEventConsumer.cs
```

Sửa constructor để inject `IProcessedEventStore`.

Full bản consumer sau khi thêm idempotency:

```csharp
using BuildingBlocks.Contracts.Events.Orders;
using MassTransit;
using NotificationWorker.Application.Abstractions;

namespace NotificationWorker.Consumers;

public sealed class OrderCreatedIntegrationEventConsumer
    : IConsumer<OrderCreatedIntegrationEvent>
{
    private readonly ILogger<OrderCreatedIntegrationEventConsumer> _logger;
    private readonly IProcessedEventStore _processedEventStore;

    public OrderCreatedIntegrationEventConsumer(
        ILogger<OrderCreatedIntegrationEventConsumer> logger,
        IProcessedEventStore processedEventStore)
    {
        _logger = logger;
        _processedEventStore = processedEventStore;
    }

    public async Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
    {
        var message = context.Message;

        if (await _processedEventStore.HasProcessedAsync(message.EventId, context.CancellationToken))
        {
            _logger.LogWarning(
                "Duplicate event skipped. EventId={EventId}, OrderId={OrderId}",
                message.EventId,
                message.OrderId);

            return;
        }

        _logger.LogInformation(
            "OrderCreatedIntegrationEvent received. EventId={EventId}, OrderId={OrderId}, CustomerId={CustomerId}, TotalAmount={TotalAmount}, Currency={Currency}, OccurredAtUtc={OccurredAtUtc}, Version={Version}",
            message.EventId,
            message.OrderId,
            message.CustomerId,
            message.TotalAmount,
            message.Currency,
            message.OccurredAtUtc,
            message.Version);

        _logger.LogInformation(
            "Simulating notification: Order {OrderId} was created for customer {CustomerId}.",
            message.OrderId,
            message.CustomerId);

        await _processedEventStore.MarkAsProcessedAsync(message.EventId, context.CancellationToken);
    }
}
```

Điểm quan trọng:

```text
Check duplicate trước khi xử lý side effect.
Mark processed sau khi xử lý thành công.
```

Câu hỏi production:

```text
Nếu gửi email thành công nhưng MarkAsProcessed fail thì sao?
Nếu MarkAsProcessed trước rồi gửi email fail thì sao?
Nếu 2 consumer xử lý cùng EventId song song thì sao?
```

Đây là lý do production idempotency cần Inbox table/unique constraint/transaction. Bài này chỉ học basic.

---

## 15. Config retry policy trong MassTransit

Mở:

```text
Services/NotificationWorker/Program.cs
```

Trong receive endpoint hiện tại:

```csharp
cfg.ReceiveEndpoint("notification-order-created", e =>
{
    e.ConfigureConsumer<OrderCreatedIntegrationEventConsumer>(context);
});
```

Sửa thành:

```csharp
cfg.ReceiveEndpoint("notification-order-created", e =>
{
    e.UseMessageRetry(r =>
    {
        r.Interval(3, TimeSpan.FromSeconds(2));
    });

    e.ConfigureConsumer<OrderCreatedIntegrationEventConsumer>(context);
});
```

Ý nghĩa:

```text
Consumer có 1 lần xử lý ban đầu.
Nếu throw exception, retry thêm 3 lần.
Mỗi retry cách nhau 2 giây.
Nếu vẫn fail sau retries, message đi vào notification-order-created_error.
```

Lưu ý:

```text
Retry trong bài này là interval retry trong consumer pipeline.
Production có thể dùng exponential backoff hoặc delayed redelivery tùy case.
```

---

## 16. Tạo cơ chế giả lập lỗi để test retry

Để test retry, ta cần làm consumer cố tình lỗi trong một số trường hợp.

Cách đơn giản: dùng config.

Sửa:

```text
Services/NotificationWorker/appsettings.Development.json
```

Nội dung mẫu:

```json
{
  "RabbitMq": {
    "Host": "localhost",
    "VirtualHost": "/",
    "UserName": "microshop",
    "Password": "microshop"
  },
  "NotificationWorker": {
    "SimulateFailure": false
  }
}
```

Nếu file đã có `RabbitMq`, merge thêm `NotificationWorker`.

Lưu ý cho Worker Service:

```text
Worker Service dùng Generic Host.
Khi chạy local, nên đảm bảo DOTNET_ENVIRONMENT=Development để appsettings.Development.json được load.
```

Nếu dùng PowerShell:

```powershell
$env:DOTNET_ENVIRONMENT="Development"
dotnet run --project Services/NotificationWorker/NotificationWorker.csproj
```

Nếu dùng bash:

```bash
DOTNET_ENVIRONMENT=Development dotnet run --project Services/NotificationWorker/NotificationWorker.csproj
```

---

## 17. Inject IConfiguration vào consumer để giả lập lỗi

Sửa consumer constructor:

```csharp
private readonly IConfiguration _configuration;

public OrderCreatedIntegrationEventConsumer(
    ILogger<OrderCreatedIntegrationEventConsumer> logger,
    IProcessedEventStore processedEventStore,
    IConfiguration configuration)
{
    _logger = logger;
    _processedEventStore = processedEventStore;
    _configuration = configuration;
}
```

Thêm đoạn sau khi check duplicate và trước khi simulate notification:

```csharp
var simulateFailure = _configuration.GetValue<bool>("NotificationWorker:SimulateFailure");

if (simulateFailure)
{
    _logger.LogWarning(
        "Simulated failure for EventId={EventId}, OrderId={OrderId}",
        message.EventId,
        message.OrderId);

    throw new InvalidOperationException("Simulated notification failure.");
}
```

Vị trí đúng:

```text
Check duplicate
→ Log received
→ Check SimulateFailure
→ Nếu true thì throw
→ Nếu false thì simulate notification
→ MarkAsProcessed
```

---

## 18. Full consumer sau khi thêm retry test + idempotency

File:

```text
Services/NotificationWorker/Consumers/OrderCreatedIntegrationEventConsumer.cs
```

```csharp
using BuildingBlocks.Contracts.Events.Orders;
using MassTransit;
using NotificationWorker.Application.Abstractions;

namespace NotificationWorker.Consumers;

public sealed class OrderCreatedIntegrationEventConsumer
    : IConsumer<OrderCreatedIntegrationEvent>
{
    private readonly ILogger<OrderCreatedIntegrationEventConsumer> _logger;
    private readonly IProcessedEventStore _processedEventStore;
    private readonly IConfiguration _configuration;

    public OrderCreatedIntegrationEventConsumer(
        ILogger<OrderCreatedIntegrationEventConsumer> logger,
        IProcessedEventStore processedEventStore,
        IConfiguration configuration)
    {
        _logger = logger;
        _processedEventStore = processedEventStore;
        _configuration = configuration;
    }

    public async Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
    {
        var message = context.Message;

        if (await _processedEventStore.HasProcessedAsync(message.EventId, context.CancellationToken))
        {
            _logger.LogWarning(
                "Duplicate event skipped. EventId={EventId}, OrderId={OrderId}",
                message.EventId,
                message.OrderId);

            return;
        }

        _logger.LogInformation(
            "OrderCreatedIntegrationEvent received. EventId={EventId}, OrderId={OrderId}, CustomerId={CustomerId}, TotalAmount={TotalAmount}, Currency={Currency}, OccurredAtUtc={OccurredAtUtc}, Version={Version}",
            message.EventId,
            message.OrderId,
            message.CustomerId,
            message.TotalAmount,
            message.Currency,
            message.OccurredAtUtc,
            message.Version);

        var simulateFailure = _configuration.GetValue<bool>("NotificationWorker:SimulateFailure");

        if (simulateFailure)
        {
            _logger.LogWarning(
                "Simulated failure for EventId={EventId}, OrderId={OrderId}",
                message.EventId,
                message.OrderId);

            throw new InvalidOperationException("Simulated notification failure.");
        }

        _logger.LogInformation(
            "Simulating notification: Order {OrderId} was created for customer {CustomerId}.",
            message.OrderId,
            message.CustomerId);

        await _processedEventStore.MarkAsProcessedAsync(message.EventId, context.CancellationToken);
    }
}
```

---

## 19. Build và chạy happy path

Build:

```bash
dotnet build Services/NotificationWorker/NotificationWorker.csproj
```

Chạy 3 terminal:

```bash
docker compose up -d rabbitmq
```

```bash
dotnet run --project Services/OrderingService/OrderingService.csproj
```

```bash
$env:DOTNET_ENVIRONMENT="Development"
dotnet run --project Services/NotificationWorker/NotificationWorker.csproj
```

Đảm bảo config:

```json
"SimulateFailure": false
```

Dùng Postman:

```text
POST {{ordering_url}}/orders
```

Expected:

```text
Order tạo thành công.
Worker consume event.
Log notification.
Không có error queue message mới do lỗi.
```

---

## 20. Test retry bằng cách bật SimulateFailure

Sửa:

```text
Services/NotificationWorker/appsettings.Development.json
```

```json
"SimulateFailure": true
```

Restart NotificationWorker.

Gửi request bằng Postman:

```text
POST {{ordering_url}}/orders
```

Expected ở NotificationWorker console:

```text
OrderCreatedIntegrationEvent received...
Simulated failure...
OrderCreatedIntegrationEvent received...
Simulated failure...
OrderCreatedIntegrationEvent received...
Simulated failure...
OrderCreatedIntegrationEvent received...
Simulated failure...
```

Vì:

```text
1 lần xử lý ban đầu + 3 retries = tối đa 4 lần Consume.
```

Sau khi retry hết, MassTransit sẽ move message sang error queue.

Mở RabbitMQ UI:

```text
Queues and Streams
```

Tìm queue:

```text
notification-order-created_error
```

Expected:

```text
[ ] Có queue error.
[ ] Có message lỗi trong error queue.
```

Bấm vào queue error và `Get messages`.

Bạn có thể thấy:

```text
Payload event.
Headers/metadata lỗi.
Exception info do MassTransit thêm.
```

---

## 21. Test recovery sau lỗi

Set lại:

```json
"SimulateFailure": false
```

Restart NotificationWorker.

Lưu ý:

```text
Message đã vào error queue sẽ không tự quay lại queue chính trong bài này.
```

Bài này chỉ cần biết:

```text
Error queue giữ lại message lỗi để debug/manual replay sau.
```

Test lại Postman tạo order mới:

```text
POST {{ordering_url}}/orders
```

Expected:

```text
Event mới được consume thành công.
```

---

## 22. Test duplicate event basic

Có 2 cách test duplicate.

### Cách A: Dùng RabbitMQ UI

Có thể publish lại cùng payload event vào exchange/queue.

Tuy nhiên với MassTransit, cách này có thể khó cho người mới vì:

```text
MassTransit message có headers/envelope/topology riêng.
Nếu publish thủ công thiếu header hoặc publish nhầm exchange, consumer có thể không nhận đúng.
```

Vì vậy cách A chỉ nên dùng nếu bạn đã quen RabbitMQ UI.

### Cách B: Tạm publish 2 event cùng EventId từ OrderingService

Cách này dễ học hơn.

Trong CreateOrderHandler, tạm thời dùng cùng EventId và publish 2 lần:

```csharp
var fixedEventId = Guid.Parse("99999999-9999-9999-9999-999999999999");

var orderCreatedEvent = new OrderCreatedIntegrationEvent
{
    EventId = fixedEventId,
    OccurredAtUtc = DateTime.UtcNow,
    Version = 1,
    OrderId = createdOrder.Id,
    CustomerId = createdOrder.CustomerId,
    TotalAmount = createdOrder.TotalAmount,
    Currency = "VND"
};

await _publishEndpoint.Publish(orderCreatedEvent, cancellationToken);
await _publishEndpoint.Publish(orderCreatedEvent, cancellationToken);
```

Expected worker log:

```text
Lần 1:
Simulating notification...

Lần 2:
Duplicate event skipped...
```

Sau khi test xong:

```text
PHẢI revert đoạn publish duplicate.
PHẢI dùng Guid.NewGuid() lại cho EventId.
PHẢI đảm bảo không commit fixed EventId vào main.
```

Cảnh báo:

```text
Debug duplicate code chỉ để học.
Không để code này trong source thật.
```

---

## 23. Postman Lab

Collection:

```text
MicroShop - Lesson 22 Retry Error Queue Idempotency
```

Environment:

| Variable | Value |
| --- | --- |
| `ordering_url` | `http://localhost:5004` |

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
Worker log notification.
No new error queue message.
```

### Request 2: Create order while SimulateFailure=true

Trước khi chạy:

```json
"SimulateFailure": true
```

Restart worker.

Gửi request như trên.

Expected:

```text
API có thể vẫn success vì publish event thành công.
Worker retry rồi fail.
Message vào notification-order-created_error.
```

### Request 3: Create order after recovery

Set:

```json
"SimulateFailure": false
```

Restart worker.

Gửi request mới.

Expected:

```text
Worker consume thành công.
```

---

## 24. Bài tập

### Bài 1: Vẽ retry flow

Vẽ:

```text
Message
→ Consumer initial attempt
→ Exception
→ Retry 1
→ Retry 2
→ Retry 3
→ Error queue
```

Giải thích:

```text
Vì sao không retry vô hạn?
Error queue giúp gì?
```

### Bài 2: Test error queue

Bật:

```json
"SimulateFailure": true
```

Gửi 1 order.

Ghi lại:

```text
Queue error tên gì?
Message count là bao nhiêu?
Payload còn EventId không?
Có exception info không?
```

### Bài 3: Test idempotency

Tạo duplicate event cùng EventId.

Ghi lại:

```text
Lần 1 worker log gì?
Lần 2 worker log gì?
Notification có bị simulate 2 lần không?
```

### Bài 4: Viết production note

Trả lời:

```text
Vì sao in-memory idempotency chưa đủ production?
Production nên dùng gì?
```

Gợi ý:

```text
Worker restart mất memory.
Scale nhiều instance không share memory.
Có race condition khi duplicate chạy song song.
Nên dùng DB Inbox/ProcessedMessages table với unique constraint theo EventId.
```

### Bài 5: Chuẩn bị bài 23

Trả lời:

```text
Bài 22 xử lý reliability phía consumer.
Bài 23 sẽ xử lý reliability phía publisher như thế nào?
```

Gợi ý:

```text
Outbox giúp tránh DB save thành công nhưng publish event fail.
```

---

## 25. Quiz nhanh

**Câu 1. Retry phù hợp nhất với lỗi nào?**

```text
A. Payload sai schema vĩnh viễn
B. Network timeout tạm thời
C. EventId rỗng do bug code
D. Business rule không hợp lệ
```

Đáp án: B

**Câu 2. MassTransit error queue dùng để làm gì?**

```text
A. Giữ message xử lý thất bại sau khi retry hết
B. Tăng tốc query database
C. Phát JWT
D. Lưu config RabbitMQ
```

Đáp án: A

**Câu 3. Idempotency trong consumer giúp gì?**

```text
A. Đảm bảo cùng event xử lý lại không tạo side effect trùng
B. Tăng số retry lên vô hạn
C. Tự động rollback RabbitMQ
D. Tự động tạo order
```

Đáp án: A

**Câu 4. In-memory idempotency có điểm yếu gì?**

```text
A. Mất dữ liệu khi worker restart, không share giữa nhiều instance, chưa chống race condition hoàn toàn
B. Không thể check EventId
C. Không compile được với C#
D. Không dùng được với RabbitMQ
```

Đáp án: A

**Câu 5. `r.Interval(3, TimeSpan.FromSeconds(2))` nghĩa là gì?**

```text
A. Tổng cộng chỉ gọi Consume 3 lần
B. Có 1 lần xử lý ban đầu, nếu lỗi thì retry thêm 3 lần
C. Retry vô hạn, mỗi lần 3 giây
D. Đưa thẳng vào error queue sau 3 giây, không retry
```

Đáp án: B

---

## 26. Production mindset

Bài 22 làm consumer đáng tin hơn nhưng chưa hoàn chỉnh production.

Đã có:

```text
Retry policy.
Error queue khi fail sau retry.
Idempotency basic theo EventId.
```

Chưa có:

```text
Persistent Inbox table.
Unique constraint EventId.
Atomic check/insert để chống race condition.
Manual replay tooling.
Alert khi error queue tăng.
Poison message dashboard.
CorrelationId/tracing đầy đủ.
Outbox phía publisher.
```

Các phần học sau:

| Chủ đề | Bài |
| --- | --- |
| Outbox Basic | Buổi 23 |
| Transactional Outbox chuẩn | Buổi 38 |
| Inbox/WebhookLog | Buổi 39 |
| Observability/tracing | Phase Observability |
| Runbook/alerting | Stage production |

Câu nhớ:

```text
Retry/error queue xử lý lỗi consumer.
Idempotency xử lý duplicate consumer.
Outbox xử lý lỗi publisher.
```

---

## 27. Lỗi hay gặp

| Lỗi | Nguyên nhân | Cách xử lý |
| --- | --- | --- |
| Không thấy retry log | Consumer không throw exception hoặc SimulateFailure=false | Kiểm tra config và restart worker |
| Không thấy error queue | Chưa retry hết hoặc endpoint name khác | Đợi retry xong, tìm queue `_error` |
| Worker vẫn xử lý thành công khi SimulateFailure=true | appsettings.Development.json không load đúng | Set `DOTNET_ENVIRONMENT=Development` |
| Duplicate không bị skip | EventId khác nhau mỗi lần | Test bằng cùng EventId |
| Duplicate skip không hoạt động sau restart | In-memory store mất dữ liệu | Đây là giới hạn expected |
| Duplicate vẫn có thể xử lý song song | HasProcessed → Process → Mark không atomic | Production cần DB unique constraint/Inbox |
| Error queue đầy message | Consumer bug hoặc config fail liên tục | Fix lỗi, manual replay sau |
| API create order fail khi worker fail | Nhầm giữa publisher failure và consumer failure | Consumer fail thường xảy ra async sau khi publish |

---

## 28. Điều kiện pass bài trong 90–120 phút

Bạn pass Buổi 22 khi:

```text
[ ] Consumer có retry policy.
[ ] SimulateFailure=true làm consumer retry rồi message vào error queue.
[ ] RabbitMQ UI thấy `notification-order-created_error`.
[ ] Consumer có check EventId idempotency basic.
[ ] Duplicate event cùng EventId bị skip.
[ ] Bạn giải thích được retry khác error queue.
[ ] Bạn giải thích được MassTransit error queue khác RabbitMQ native DLX/DLQ ở mức cơ bản.
[ ] Bạn giải thích được idempotency là gì.
[ ] Bạn nói được vì sao in-memory idempotency chưa đủ production.
[ ] Bạn nói được bài 22 không sửa publisher, Outbox để bài 23.
```

Nếu hôm nay chỉ làm được:

```text
SimulateFailure=true
→ retry
→ error queue
```

và:

```text
Duplicate EventId
→ skip
```

là đạt mục tiêu chính.

---

## 29. Không làm trong bài này

Không làm:

```text
[ ] Không làm Outbox.
[ ] Không làm database Inbox table.
[ ] Không làm manual replay tool.
[ ] Không gửi email thật.
[ ] Không làm alerting.
[ ] Không làm distributed tracing.
[ ] Không làm Saga.
[ ] Không làm Inventory/Shipping.
```

Lý do:

```text
Bài 22 chỉ xử lý consumer-side reliability ở mức basic.
Publisher-side reliability để Buổi 23.
```

---

## 30. Điều kiện mở khóa Buổi 23

Bạn có thể sang Buổi 23 khi:

```text
[ ] NotificationWorker retry được khi lỗi.
[ ] Message lỗi đi vào error queue sau retry.
[ ] Consumer skip được duplicate EventId ở mức basic.
[ ] Bạn hiểu Outbox sẽ giải quyết vấn đề DB save thành công nhưng publish fail.
```

Buổi 23 sẽ học:

```text
Outbox Basic + Background Publisher Intro
```

Mục tiêu Buổi 23:

```text
OrderingService không publish trực tiếp ngay trong handler nữa.
Thay vào đó, lưu event vào Outbox table.
Background publisher đọc Outbox và publish sang RabbitMQ.
```
