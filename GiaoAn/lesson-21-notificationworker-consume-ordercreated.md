---
lesson: 21
title: "NotificationWorker - Consume OrderCreatedIntegrationEvent"
duration: "90–120 phút"
phase: "Phase 1.5 - RabbitMQ + Reliability Basic"
project: "MicroShop"
testing: "Postman-first"
type: "lesson"
roadmap_alignment: "Buổi 21 đúng roadmap: NotificationWorker. Buổi 22 mới là Retry/DLQ + Idempotency Basic."
---

# Buổi 21: NotificationWorker - Consume OrderCreatedIntegrationEvent

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
OrderingService publish shared contract mới.
```

Bây giờ Buổi 21 sẽ hoàn thiện vòng event-driven đầu tiên:

```text
OrderingService publish OrderCreatedIntegrationEvent
→ RabbitMQ
→ NotificationWorker consume event
→ ghi log/mô phỏng gửi notification
```

Câu nhớ:

```text
Bài 19 là publish.
Bài 20 là contract.
Bài 21 là consume.
```

---

## 1. Mục tiêu bài học

Trong 90–120 phút, mục tiêu là:

```text
[ ] Hiểu Worker Service là gì trong .NET.
[ ] Tạo project NotificationWorker.
[ ] Reference BuildingBlocks.Contracts.
[ ] Cài MassTransit + MassTransit.RabbitMQ cho NotificationWorker.
[ ] Tạo OrderCreatedIntegrationEventConsumer.
[ ] Config NotificationWorker consume event từ RabbitMQ.
[ ] Chạy OrderingService + NotificationWorker + RabbitMQ.
[ ] Dùng Postman tạo order.
[ ] Thấy NotificationWorker nhận event và log notification.
[ ] Hiểu vì sao consumer cần idempotency/retry/DLQ ở bài sau.
```

Bài này chỉ làm consumer baseline.

Chưa làm:

```text
[ ] Retry policy nâng cao.
[ ] Dead-letter queue.
[ ] Idempotent consumer.
[ ] Inbox table.
[ ] Email provider thật.
[ ] Outbox.
```

---

## 2. Vì sao cần NotificationWorker?

Sau Buổi 20, OrderingService đã publish event chuẩn:

```text
OrderCreatedIntegrationEvent
```

Nhưng nếu chưa có consumer, RabbitMQ mới chỉ đang nhận message. Chưa có service nào xử lý side effect.

Trong hệ thống thật, sau khi order được tạo có thể cần:

```text
Gửi email xác nhận đơn hàng.
Gửi push notification.
Ghi audit log.
Update read model.
Kích hoạt workflow async khác.
```

Không nên nhồi những việc này vào `CreateOrderHandler`.

Thay vào đó:

```text
OrderingService chỉ publish OrderCreatedIntegrationEvent.
NotificationWorker nhận event và xử lý notification.
```

Điều này giúp:

```text
OrderingService nhẹ hơn.
Notification logic tách khỏi order creation.
Có thể scale worker riêng.
Có thể retry xử lý notification mà không ảnh hưởng request tạo order.
```

---

## 3. Worker Service là gì?

Worker Service là app chạy nền, không nhất thiết expose HTTP API.

| Loại app | Vai trò |
| --- | --- |
| Web API | Nhận HTTP request từ client/service |
| Worker Service | Chạy background task, consume queue/event, xử lý job |

NotificationWorker trong bài này là:

```text
Một .NET Worker Service
→ kết nối RabbitMQ qua MassTransit
→ consume OrderCreatedIntegrationEvent
→ log message mô phỏng gửi notification
```

Flow:

```text
RabbitMQ có message
→ MassTransit đưa message vào Consumer
→ Consumer xử lý
→ log ra console
```

---

## 4. Consumer là gì?

Consumer là class nhận và xử lý message/event.

Trong MassTransit, consumer thường implement:

```csharp
IConsumer<TMessage>
```

Ví dụ:

```csharp
public sealed class OrderCreatedIntegrationEventConsumer
    : IConsumer<OrderCreatedIntegrationEvent>
{
    public Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
    {
        var message = context.Message;
        return Task.CompletedTask;
    }
}
```

Ý nghĩa:

```text
TMessage = event/message cần consume.
ConsumeContext = context chứa message + metadata.
context.Message = payload event.
```

---

## 5. Target architecture sau bài này

Trước Buổi 21:

```text
OrderingService
→ RabbitMQ
```

Sau Buổi 21:

```text
Postman
→ OrderingService
→ RabbitMQ
→ NotificationWorker
→ Console log notification
```

Sơ đồ:

```text
POST /orders
    |
    v
OrderingService
    |
    | Publish OrderCreatedIntegrationEvent
    v
RabbitMQ
    |
    | Deliver message
    v
NotificationWorker
    |
    | Consume event
    v
Log "Send notification for order..."
```

---

## 6. Chuẩn bị trước khi làm

Bạn cần có sẵn từ bài 19–20:

```text
[ ] RabbitMQ chạy được bằng Docker Compose.
[ ] OrderingService đã config MassTransit.
[ ] BuildingBlocks.Contracts tồn tại.
[ ] OrderCreatedIntegrationEvent nằm trong BuildingBlocks.Contracts.
[ ] OrderingService publish OrderCreatedIntegrationEvent.
```

Nếu chưa chắc, build trước:

```bash
dotnet build
```

Chạy RabbitMQ:

```bash
docker compose up -d rabbitmq
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

## 7. Tạo NotificationWorker project

Đứng ở root solution `MicroShop`.

Tạo worker:

```bash
dotnet new worker -n NotificationWorker -o Services/NotificationWorker
```

Add vào solution:

```bash
dotnet sln add Services/NotificationWorker/NotificationWorker.csproj
```

Build thử:

```bash
dotnet build Services/NotificationWorker/NotificationWorker.csproj
```

Mục tiêu:

```text
[ ] Có project Services/NotificationWorker.
[ ] Có Worker.cs.
[ ] Có Program.cs.
[ ] Build pass.
```

---

## 8. Add reference BuildingBlocks.Contracts

NotificationWorker cần dùng chung event contract với OrderingService.

Chạy:

```bash
dotnet add Services/NotificationWorker/NotificationWorker.csproj reference BuildingBlocks/BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj
```

Build:

```bash
dotnet build Services/NotificationWorker/NotificationWorker.csproj
```

Nếu lỗi không tìm thấy `BuildingBlocks`, kiểm tra path project reference.

Điểm cần nhớ:

```text
NotificationWorker reference BuildingBlocks.Contracts.
NotificationWorker không reference OrderingService.
```

Đúng:

```text
NotificationWorker → BuildingBlocks.Contracts
OrderingService → BuildingBlocks.Contracts
```

Sai:

```text
NotificationWorker → OrderingService
```

---

## 9. Cài MassTransit cho NotificationWorker

Chạy:

```bash
dotnet add Services/NotificationWorker/NotificationWorker.csproj package MassTransit
dotnet add Services/NotificationWorker/NotificationWorker.csproj package MassTransit.RabbitMQ
```

Build:

```bash
dotnet build Services/NotificationWorker/NotificationWorker.csproj
```

Pass khi:

```text
[ ] Không lỗi namespace MassTransit.
[ ] Project build pass.
```

---

## 10. Thêm RabbitMQ config cho NotificationWorker

Tạo hoặc sửa file:

```text
Services/NotificationWorker/appsettings.Development.json
```

Nội dung:

```json
{
  "RabbitMq": {
    "Host": "localhost",
    "VirtualHost": "/",
    "UserName": "microshop",
    "Password": "microshop"
  }
}
```

Nếu worker chạy trong Docker Compose cùng network RabbitMQ, host có thể là:

```text
rabbitmq
```

Nhưng trong bài này, nếu chạy worker local bằng `dotnet run`, dùng:

```text
localhost
```

---

## 11. Tạo RabbitMqOptions

Tạo folder:

```text
Services/NotificationWorker/Infrastructure/Messaging
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force Services/NotificationWorker/Infrastructure/Messaging
```

Tạo file:

```text
Services/NotificationWorker/Infrastructure/Messaging/RabbitMqOptions.cs
```

```csharp
namespace NotificationWorker.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public string Host { get; init; } = "localhost";
    public string VirtualHost { get; init; } = "/";
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
}
```

---

## 12. Tạo consumer folder

Tạo folder:

```text
Services/NotificationWorker/Consumers
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force Services/NotificationWorker/Consumers
```

---

## 13. Tạo OrderCreatedIntegrationEventConsumer

Tạo file:

```text
Services/NotificationWorker/Consumers/OrderCreatedIntegrationEventConsumer.cs
```

```csharp
using BuildingBlocks.Contracts.Events.Orders;
using MassTransit;

namespace NotificationWorker.Consumers;

public sealed class OrderCreatedIntegrationEventConsumer
    : IConsumer<OrderCreatedIntegrationEvent>
{
    private readonly ILogger<OrderCreatedIntegrationEventConsumer> _logger;

    public OrderCreatedIntegrationEventConsumer(
        ILogger<OrderCreatedIntegrationEventConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
    {
        var message = context.Message;

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

        return Task.CompletedTask;
    }
}
```

Điểm cần nhớ:

```text
Consumer chưa gửi email thật.
Consumer chỉ log để chứng minh đã consume event.
```

Lý do chưa gửi email thật:

```text
Email provider/config/template/retry là scope khác.
Bài này tập trung RabbitMQ consumer baseline.
```

---

## 14. Sửa Program.cs của NotificationWorker

Mở:

```text
Services/NotificationWorker/Program.cs
```

Template ban đầu có thể giống:

```csharp
using NotificationWorker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

Sửa thành:

```csharp
using MassTransit;
using NotificationWorker.Consumers;
using NotificationWorker.Infrastructure.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedIntegrationEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqOptions = context
            .GetRequiredService<IConfiguration>()
            .GetSection("RabbitMq")
            .Get<RabbitMqOptions>()
            ?? new RabbitMqOptions();

        cfg.Host(
            rabbitMqOptions.Host,
            rabbitMqOptions.VirtualHost,
            h =>
            {
                h.Username(rabbitMqOptions.UserName);
                h.Password(rabbitMqOptions.Password);
            });

        cfg.ReceiveEndpoint("notification-order-created", e =>
        {
            e.ConfigureConsumer<OrderCreatedIntegrationEventConsumer>(context);
        });
    });
});

var host = builder.Build();

host.Run();
```

Giải thích:

```text
x.AddConsumer<OrderCreatedIntegrationEventConsumer>()
→ đăng ký consumer class với DI/MassTransit.

cfg.ReceiveEndpoint("notification-order-created", ...)
→ tạo queue endpoint tên notification-order-created.

e.ConfigureConsumer(...)
→ nối queue này với consumer xử lý OrderCreatedIntegrationEvent.
```

Queue sẽ xuất hiện trong RabbitMQ:

```text
notification-order-created
```

---

## 15. Có cần giữ Worker.cs không?

Template worker có thể tạo file:

```text
Services/NotificationWorker/Worker.cs
```

Nếu Program.cs đã dùng MassTransit consumer, bạn có thể:

### Cách đơn giản

Giữ `Worker.cs` nhưng không register:

```csharp
// Không gọi builder.Services.AddHostedService<Worker>();
```

### Cách gọn hơn

Xóa file `Worker.cs` nếu không dùng:

```powershell
Remove-Item Services/NotificationWorker/Worker.cs
```

Bài này khuyên:

```text
Xóa Worker.cs để project gọn, vì MassTransit consumer đã là worker xử lý message.
```

---

## 16. Build NotificationWorker

Chạy:

```bash
dotnet build Services/NotificationWorker/NotificationWorker.csproj
```

Nếu lỗi:

```text
The type or namespace name 'BuildingBlocks' could not be found
```

Kiểm tra:

```text
[ ] Đã add project reference BuildingBlocks.Contracts.
[ ] using đúng BuildingBlocks.Contracts.Events.Orders.
```

Nếu lỗi MassTransit:

```text
The type or namespace name 'MassTransit' could not be found
```

Kiểm tra:

```text
[ ] Đã cài MassTransit.
[ ] Đã cài MassTransit.RabbitMQ.
```

---

## 17. Chạy hệ thống để test

Cần chạy 3 phần:

```text
1. RabbitMQ
2. OrderingService
3. NotificationWorker
```

Terminal 1:

```bash
docker compose up -d rabbitmq
```

Terminal 2:

```bash
dotnet run --project Services/OrderingService/OrderingService.csproj
```

Terminal 3:

```bash
dotnet run --project Services/NotificationWorker/NotificationWorker.csproj
```

Khi NotificationWorker start thành công, log có thể có dạng:

```text
Configured endpoint notification-order-created
Bus started: rabbitmq://localhost/
```

Mở RabbitMQ UI:

```text
http://localhost:15672
```

Kiểm tra queue:

```text
Queues and Streams
→ notification-order-created
```

Pass khi:

```text
[ ] Queue notification-order-created xuất hiện.
[ ] Consumer count > 0 khi worker đang chạy.
```

---

## 18. Test bằng Postman

Tạo collection:

```text
MicroShop - Lesson 21 NotificationWorker
```

Environment:

| Variable | Value |
| --- | --- |
| `ordering_url` | `http://localhost:5004` |
| `customer_id` | `11111111-1111-1111-1111-111111111111` |

Request:

```text
POST {{ordering_url}}/orders
```

Body mẫu tham khảo:

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

Expected API:

```text
HTTP 201 Created hoặc 200 OK tùy endpoint hiện tại.
Có orderId.
```

Expected NotificationWorker console:

```text
OrderCreatedIntegrationEvent received. EventId=..., OrderId=..., CustomerId=..., TotalAmount=..., Currency=...
Simulating notification: Order ... was created for customer ...
```

Expected RabbitMQ UI:

```text
Queue notification-order-created có consumer.
Message được consume xong nên Ready có thể về 0.
```

Lưu ý:

```text
Nếu worker đang chạy, message có thể được consume rất nhanh.
Ready messages = 0 không có nghĩa là lỗi.
Hãy xem log worker.
```

---

## 19. Vì sao Ready messages có thể bằng 0?

Khi queue có consumer đang online:

```text
RabbitMQ nhận message
→ deliver ngay cho consumer
→ consumer xử lý xong
→ message được ack
→ queue không còn Ready message
```

Nên nếu bạn thấy:

```text
Ready = 0
Unacked = 0
```

nhưng console worker có log nhận event, tức là đã thành công.

Nếu muốn quan sát message nằm trong queue:

```text
Tắt NotificationWorker
Tạo order bằng Postman
Vào RabbitMQ UI xem queue
Ready messages sẽ tăng
Bật lại NotificationWorker
Message được consume
Ready giảm về 0
```

Đây là bài test rất tốt để hiểu queue.

---

## 20. Test case nên làm

### Test case 1: Worker online

```text
RabbitMQ chạy.
OrderingService chạy.
NotificationWorker chạy.
POST /orders.
```

Kỳ vọng:

```text
API trả success.
Worker log nhận event.
Queue ready có thể vẫn 0 vì đã consume.
```

### Test case 2: Worker offline

```text
RabbitMQ chạy.
OrderingService chạy.
NotificationWorker tắt.
POST /orders.
```

Kỳ vọng:

```text
API trả success.
Queue notification-order-created có Ready message tăng lên.
```

Sau đó bật worker:

```bash
dotnet run --project Services/NotificationWorker/NotificationWorker.csproj
```

Kỳ vọng:

```text
Worker consume message tồn trong queue.
Ready message giảm.
Console log event.
```

### Test case 3: Nhiều order liên tiếp

Gửi POST /orders 3 lần.

Kỳ vọng:

```text
Worker log 3 event.
Mỗi event có EventId khác nhau.
OrderId khác nhau.
```

---

## 21. Checklist hoàn thành bài

```text
[ ] Tạo project NotificationWorker.
[ ] Add project vào solution.
[ ] Add reference BuildingBlocks.Contracts.
[ ] Cài MassTransit.
[ ] Cài MassTransit.RabbitMQ.
[ ] Tạo RabbitMqOptions.
[ ] Tạo OrderCreatedIntegrationEventConsumer.
[ ] Program.cs đăng ký AddMassTransit.
[ ] Program.cs đăng ký consumer.
[ ] Program.cs cấu hình ReceiveEndpoint notification-order-created.
[ ] Build NotificationWorker pass.
[ ] Chạy RabbitMQ.
[ ] Chạy OrderingService.
[ ] Chạy NotificationWorker.
[ ] RabbitMQ UI thấy queue notification-order-created.
[ ] Queue có consumer khi worker chạy.
[ ] Postman POST /orders thành công.
[ ] NotificationWorker log nhận OrderCreatedIntegrationEvent.
[ ] Test worker offline → message nằm trong queue.
[ ] Bật worker lại → message được consume.
```

---

## 22. Bài tập

### Bài 1: Vẽ flow consume event

Vẽ lại flow:

```text
POST /orders
→ OrderingService
→ Publish OrderCreatedIntegrationEvent
→ RabbitMQ
→ Queue notification-order-created
→ OrderCreatedIntegrationEventConsumer
→ Log notification
```

Giải thích:

```text
Publisher là ai?
Broker là gì?
Queue là gì?
Consumer là ai?
```

### Bài 2: Test worker offline

Làm đúng flow:

```text
1. Tắt NotificationWorker.
2. POST /orders.
3. Xem RabbitMQ queue có Ready message tăng không.
4. Bật NotificationWorker.
5. Xem console worker có log event không.
6. Xem Ready message giảm không.
```

Ghi lại kết quả.

### Bài 3: Consumer không reference OrderingService

Trả lời:

```text
Vì sao NotificationWorker không nên reference OrderingService?
Vì sao nó chỉ cần BuildingBlocks.Contracts?
```

Gợi ý:

```text
Worker chỉ cần event contract.
Nếu reference OrderingService, worker bị coupling vào service publisher.
```

### Bài 4: Nhìn trước bài 22

Trả lời:

```text
Nếu consumer xử lý lỗi thì message sẽ ra sao?
Nếu cùng một event bị deliver lại thì làm sao tránh xử lý trùng?
```

Gợi ý:

```text
Bài 22 sẽ học Retry/DLQ + Idempotency Basic.
```

### Bài 5: Thử log rõ hơn

Sửa consumer log thêm:

```text
EventId
OrderId
CustomerId
TotalAmount
Currency
Version
```

Mục tiêu:

```text
Log đủ để debug event flow.
```

---

## 23. Quiz nhanh

**Câu 1. NotificationWorker trong bài 21 là gì?**

```text
A. Web API nhận HTTP request tạo order
B. Worker Service consume event từ RabbitMQ
C. Database migration tool
D. API Gateway
```

Đáp án: B

**Câu 2. NotificationWorker nên reference project nào để biết OrderCreatedIntegrationEvent?**

```text
A. OrderingService
B. BuildingBlocks.Contracts
C. CatalogService
D. PaymentService
```

Đáp án: B

**Câu 3. MassTransit consumer implement interface nào?**

```text
A. IRequestHandler<T>
B. IConsumer<TMessage>
C. IHostedApi<T>
D. IRepository<T>
```

Đáp án: B

**Câu 4. Vì sao Ready messages có thể bằng 0 dù event đã được xử lý?**

```text
A. Vì message đã được consumer xử lý và ack xong
B. Vì RabbitMQ không nhận message
C. Vì Postman không gửi request
D. Vì event không có EventId
```

Đáp án: A

**Câu 5. Bài 22 sẽ học gì tiếp?**

```text
A. Retry/DLQ + Idempotency Basic
B. BuildingBlocks.Contracts
C. IdentityService + JWT
D. Catalog Clean Architecture
```

Đáp án: A

---

## 24. Production mindset

Bài 21 tạo được event-driven flow end-to-end đầu tiên:

```text
Publisher → RabbitMQ → Consumer
```

Nhưng vẫn chưa production-safe.

Các vấn đề chưa xử lý:

```text
Consumer xử lý lỗi thì retry thế nào?
Message lỗi nhiều lần thì đưa vào đâu?
Cùng event bị deliver lại thì làm sao tránh gửi notification trùng?
Làm sao biết event nào đã xử lý?
Làm sao trace một order qua publisher/consumer?
```

Các bài sau xử lý dần:

| Vấn đề | Bài |
| --- | --- |
| Retry policy | Buổi 22 |
| Dead-letter/error queue | Buổi 22 |
| Idempotent consumer basic | Buổi 22 |
| Outbox publisher | Buổi 23 |
| Transactional Outbox chuẩn | Buổi 38 |
| Inbox/WebhookLog | Buổi 39 |
| Observability/tracing | Phase Observability |

Câu nhớ:

```text
Consumer chạy được là bước đầu.
Consumer đáng tin cần retry, DLQ và idempotency.
```

---

## 25. Lỗi hay gặp

| Lỗi | Nguyên nhân | Cách xử lý |
| --- | --- | --- |
| Worker không build | Thiếu MassTransit package hoặc BuildingBlocks reference | Add package/reference |
| Không thấy queue | Worker chưa chạy hoặc ReceiveEndpoint chưa config | Chạy worker, kiểm tra endpoint name |
| Queue có message nhưng worker không consume | Consumer chưa registered hoặc bind sai message type | Kiểm tra AddConsumer và ConfigureConsumer |
| Worker connect RabbitMQ fail | Sai Host/User/Pass hoặc RabbitMQ chưa chạy | Kiểm tra appsettings và Docker |
| Ready = 0 nên tưởng lỗi | Worker consume quá nhanh | Xem console log hoặc tắt worker để test offline |
| Consumer không nhận sau khi đổi event contract | Publisher/consumer dùng khác contract/namespace/version | Cùng reference BuildingBlocks.Contracts |
| Notification log bị lặp | Message bị redeliver/retry hoặc gửi nhiều order | Bài 22 học idempotency |
| Worker reference OrderingService | Coupling sai | Chỉ reference BuildingBlocks.Contracts |

---

## 26. Điều kiện pass bài trong 90–120 phút

Bạn pass Buổi 21 khi:

```text
[ ] NotificationWorker được tạo và build pass.
[ ] NotificationWorker reference BuildingBlocks.Contracts.
[ ] NotificationWorker consume OrderCreatedIntegrationEvent bằng MassTransit.
[ ] RabbitMQ có queue notification-order-created.
[ ] Postman tạo order thành công.
[ ] Console NotificationWorker log được event.
[ ] Test worker offline/online để hiểu Ready messages.
[ ] Bạn giải thích được vì sao NotificationWorker không reference OrderingService.
[ ] Bạn biết bài sau cần retry/DLQ/idempotency.
```

Nếu chỉ làm được:

```text
POST /orders
→ Worker log nhận event
```

là đã đạt mục tiêu chính của bài.

---

## 27. Không làm trong bài này

Không làm:

```text
[ ] Không gửi email thật.
[ ] Không tích hợp SMTP/SendGrid.
[ ] Không làm Retry/DLQ.
[ ] Không làm Idempotency.
[ ] Không làm Outbox.
[ ] Không làm Inbox table.
[ ] Không làm distributed tracing.
[ ] Không thêm Inventory/Shipping.
[ ] Không đổi checkout flow.
```

Lý do:

```text
Bài 21 chỉ làm consumer baseline.
Các phần reliability xử lý ở bài 22–23.
```

---

## 28. Điều kiện mở khóa Buổi 22

Bạn có thể sang Buổi 22 khi:

```text
[ ] NotificationWorker consume được OrderCreatedIntegrationEvent.
[ ] RabbitMQ UI thấy queue notification-order-created.
[ ] Bạn hiểu worker online/offline ảnh hưởng Ready messages thế nào.
[ ] Bạn hiểu consumer có thể xử lý lỗi hoặc xử lý trùng event.
```

Buổi 22 sẽ học:

```text
Retry/DLQ + Idempotency Basic
```

Mục tiêu Buổi 22:

```text
Làm consumer đáng tin hơn:
- retry khi lỗi tạm thời
- đưa message lỗi vào error/DLQ
- chống xử lý trùng event ở mức basic
```
