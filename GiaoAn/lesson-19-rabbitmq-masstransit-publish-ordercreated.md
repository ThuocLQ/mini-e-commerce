---
lesson: 19
title: "RabbitMQ + MassTransit - Publish OrderCreatedEvent"
duration: "90–120 phút"
phase: "Phase 1.5 - RabbitMQ + Reliability Basic"
project: "MicroShop"
testing: "Postman-first"
type: "lesson"
roadmap_alignment: "Buổi 19 đúng roadmap: RabbitMQ + MassTransit. Buổi 20 mới là BuildingBlocks.Contracts + Event Design."
---

# Buổi 19: RabbitMQ + MassTransit - Publish OrderCreatedEvent

## 0. Sửa lại cho đúng lộ trình

Bài này thay thế bản Buổi 19 cũ dùng `RabbitMQ.Client` tự viết `RabbitMqEventBus`.

Theo roadmap hiện tại:

```text
Buổi 19: RabbitMQ + MassTransit
Buổi 20: BuildingBlocks.Contracts + Event Design
Buổi 21: NotificationWorker
Buổi 22: Retry/DLQ + Idempotency Basic
Buổi 23: Outbox Basic + Background Publisher Intro
```

Vì vậy Buổi 19 cần đi đúng hướng:

```text
OrderingService
→ publish OrderCreatedEvent bằng MassTransit
→ RabbitMQ nhận message
→ kiểm tra bằng RabbitMQ Management UI
```

Bài này **chưa** tách `BuildingBlocks.Contracts` sâu, vì phần đó là Buổi 20.  
Bài này **chưa** tạo `NotificationWorker`, vì phần đó là Buổi 21.  
Bài này **chưa** làm retry/DLQ/idempotency/outbox, vì đó là Buổi 22–23.

Câu nhớ:

```text
Buổi 19 mở cửa event-driven bằng MassTransit.
Buổi 20 mới chuẩn hóa event contract.
Buổi 21 mới tạo worker consume event.
```

---

## 1. Mục tiêu bài học

Trong 90–120 phút, mục tiêu là:

```text
[ ] Hiểu RabbitMQ dùng để làm gì trong MicroShop.
[ ] Hiểu MassTransit giúp gì khi dùng RabbitMQ.
[ ] Chạy RabbitMQ bằng Docker Compose.
[ ] Cài MassTransit + MassTransit.RabbitMQ vào OrderingService.
[ ] Tạo event tạm thời OrderCreatedEvent trong OrderingService.
[ ] Config MassTransit trong OrderingService.
[ ] Publish OrderCreatedEvent sau khi tạo order.
[ ] Tạo queue test trong RabbitMQ UI.
[ ] Gửi request bằng Postman và thấy message đi vào RabbitMQ.
[ ] Hiểu risk hiện tại: save DB thành công nhưng publish event fail.
```

Bài này chưa cần consumer. Chỉ cần publish event thành công là đạt.

---

## 2. Vì sao cần RabbitMQ ở giai đoạn này?

Sau Phase 1.4, MicroShop đã có:

```text
OrderingService
Checkout Flow
DiscountService
PaymentService
Payment Webhook Intro
```

Nhưng hệ thống vẫn đang thiên về REST synchronous.

Ví dụ sau khi order được tạo, có nhiều việc có thể xảy ra:

```text
Gửi email xác nhận order.
Gửi notification.
Ghi audit log.
Update read model.
Kích hoạt payment workflow.
Reserve inventory.
```

Nếu `CreateOrderHandler` gọi trực tiếp từng service:

```text
CreateOrderHandler
→ Save Order
→ Call NotificationService
→ Call PaymentService
→ Call InventoryService
→ Call ShippingService
```

thì handler sẽ bị coupling mạnh và dễ fail dây chuyền.

RabbitMQ giúp chuyển sang hướng:

```text
OrderingService tạo order
→ publish OrderCreatedEvent
→ service/worker khác xử lý sau
```

Ý tưởng quan trọng:

```text
OrderingService chỉ cần nói: "Order đã được tạo."
OrderingService không cần biết ai sẽ làm gì với sự kiện đó.
```

---

## 3. RabbitMQ là gì?

RabbitMQ là message broker.

Nói dễ hiểu:

```text
Service A gửi message vào RabbitMQ.
RabbitMQ giữ message.
Service B/C/D đọc message và xử lý.
```

Trong MicroShop, RabbitMQ phù hợp cho các workflow nghiệp vụ async:

```text
OrderCreated
PaymentSucceeded
PaymentFailed
NotificationRequested
```

RabbitMQ không thay thế REST hoàn toàn.

Dùng REST khi:

```text
Cần response ngay.
Cần query dữ liệu.
Cần command sync có kết quả tức thì.
```

Dùng event khi:

```text
Một chuyện đã xảy ra.
Service khác có thể xử lý sau.
Không muốn service tạo event bị coupling với service xử lý event.
```

---

## 4. MassTransit là gì?

MassTransit là framework .NET giúp làm message-based/distributed applications dễ hơn.

Nếu dùng RabbitMQ.Client trực tiếp, mình phải tự xử lý nhiều thứ:

```text
Connection
Channel
Exchange
Queue
Binding
Serialize JSON
Publish
Consume
Retry
Error queue
Convention
Headers
```

MassTransit giúp có programming model cao hơn:

```text
Define message contract.
Inject IPublishEndpoint.
Publish message.
Configure RabbitMQ transport.
Sau này thêm consumer/retry/DLQ/outbox dễ hơn.
```

Trong bài này, mình chỉ dùng phần đơn giản nhất:

```text
IPublishEndpoint.Publish(...)
```

---

## 5. REST sync vs Event async

| Tiêu chí | REST synchronous | Event asynchronous |
| --- | --- | --- |
| Bản chất | Service gọi service khác và chờ response | Service publish event, service khác xử lý sau |
| Coupling | Cao hơn | Thấp hơn |
| Response cho user | Có ngay | Không chờ consumer xử lý |
| Phù hợp | Query, command cần kết quả ngay | Notification, audit, projection, workflow async |
| Failure | Downstream fail có thể làm request fail | Broker giữ message, consumer xử lý sau |
| Độ khó | Dễ hiểu lúc đầu | Cần học broker/retry/idempotency/outbox |

Câu nhớ:

```text
REST hỏi hoặc ra lệnh trực tiếp.
Event thông báo rằng một chuyện đã xảy ra.
```

---

## 6. Integration Event trong bài này

Bài này tạo event:

```text
OrderCreatedEvent
```

Đây là event tạm nằm trong OrderingService để học MassTransit trước.

Buổi 20 sẽ nâng cấp thành:

```text
BuildingBlocks.Contracts
→ OrderCreatedIntegrationEvent
→ event naming/versioning
→ shared contract đúng hơn
```

Event nên mô tả chuyện đã xảy ra:

```text
OrderCreatedEvent
PaymentSucceededEvent
PaymentFailedEvent
```

Không nên đặt event như command:

```text
CreateOrderEvent
SendEmailNowEvent
ProcessPaymentEvent
```

Vì event không phải mệnh lệnh. Event là thông báo quá khứ:

```text
Order đã được tạo.
```

---

## 7. Target architecture sau bài này

Trước bài 19:

```text
Client/Postman
→ OrderingService
→ Database
```

Sau bài 19:

```text
Client/Postman
→ OrderingService
→ Database
→ MassTransit
→ RabbitMQ
→ OrderCreatedEvent
```

Sơ đồ:

```text
POST /orders
    |
    v
CreateOrderHandler
    |
    | save order
    v
Order DB
    |
    | publish event
    v
MassTransit
    |
    v
RabbitMQ exchange/message
```

---

## 8. Chạy RabbitMQ bằng Docker Compose

Nếu project đã có `docker-compose.yml`, thêm service RabbitMQ:

```yaml
services:
  rabbitmq:
    image: rabbitmq:3-management
    container_name: microshop-rabbitmq
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: microshop
      RABBITMQ_DEFAULT_PASS: microshop
```

Ý nghĩa port:

```text
5672  = AMQP port, app .NET kết nối RabbitMQ qua port này.
15672 = RabbitMQ Management UI.
```

Chạy:

```bash
docker compose up -d rabbitmq
```

Kiểm tra:

```bash
docker ps
```

Mở UI:

```text
http://localhost:15672
```

Login:

```text
username: microshop
password: microshop
```

Pass khi:

```text
[ ] RabbitMQ container running.
[ ] Mở được RabbitMQ Management UI.
[ ] Login được bằng microshop/microshop.
```

---

## 9. Thêm config RabbitMQ cho OrderingService

Sửa file:

```text
Services/OrderingService/appsettings.Development.json
```

Thêm section:

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

Nếu file đã có config khác, merge JSON cho đúng.

Ví dụ:

```json
{
  "ConnectionStrings": {
    "OrderingDb": "Data Source=ordering.db"
  },
  "RabbitMq": {
    "Host": "localhost",
    "VirtualHost": "/",
    "UserName": "microshop",
    "Password": "microshop"
  }
}
```

Lưu ý:

```text
Host = localhost khi chạy service từ máy local.
Nếu OrderingService chạy trong Docker Compose cùng network với RabbitMQ, Host thường là rabbitmq.
```

---

## 10. Cài MassTransit package

Cài vào OrderingService:

```bash
dotnet add Services/OrderingService/OrderingService.csproj package MassTransit
dotnet add Services/OrderingService/OrderingService.csproj package MassTransit.RabbitMQ
```

Build kiểm tra:

```bash
dotnet build Services/OrderingService/OrderingService.csproj
```

Pass khi:

```text
[ ] Project restore/build pass.
[ ] Không dùng RabbitMQ.Client trực tiếp ở bài này.
```

---

## 11. Tạo folder Events trong OrderingService

Tạo folder:

```text
Services/OrderingService/Application/IntegrationEvents
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force Services/OrderingService/Application/IntegrationEvents
```

Lưu ý:

```text
Buổi 19 để event tạm trong OrderingService.
Buổi 20 sẽ di chuyển/chuẩn hóa event sang BuildingBlocks.Contracts.
```

---

## 12. Tạo OrderCreatedEvent

Tạo file:

```text
Services/OrderingService/Application/IntegrationEvents/OrderCreatedEvent.cs
```

```csharp
namespace OrderingService.Application.IntegrationEvents;

public sealed record OrderCreatedEvent
{
    public Guid EventId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "VND";
}
```

Giải thích:

| Field | Ý nghĩa |
| --- | --- |
| `EventId` | ID duy nhất của event |
| `OccurredAtUtc` | Thời điểm event xảy ra |
| `OrderId` | Order vừa tạo |
| `CustomerId` | Customer của order |
| `TotalAmount` | Tổng tiền |
| `Currency` | Tiền tệ |

Vì sao có `EventId`?

```text
Sau này dùng để idempotency, chống xử lý trùng event.
Bài này chưa xử lý duplicate, nhưng contract nên có sẵn.
```

Vì sao chưa dùng `Version`?

```text
Buổi 20 mới học event design/versioning kỹ hơn.
Bài 19 giữ event tối thiểu để publish được trước.
```

---

## 13. Tạo RabbitMqOptions

Tạo folder:

```text
Services/OrderingService/Infrastructure/Messaging
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force Services/OrderingService/Infrastructure/Messaging
```

Tạo file:

```text
Services/OrderingService/Infrastructure/Messaging/RabbitMqOptions.cs
```

```csharp
namespace OrderingService.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public string Host { get; init; } = "localhost";
    public string VirtualHost { get; init; } = "/";
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
}
```

---

## 14. Config MassTransit trong Program.cs

Sửa file:

```text
Services/OrderingService/Program.cs
```

Thêm using:

```csharp
using MassTransit;
using OrderingService.Infrastructure.Messaging;
```

Thêm đăng ký options:

```csharp
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));
```

Thêm MassTransit:

```csharp
builder.Services.AddMassTransit(x =>
{
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
    });
});
```

Ví dụ vị trí trong `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddMassTransit(x =>
{
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
    });
});
```

MassTransit sẽ tự đăng ký các service cần thiết, trong đó có:

```text
IPublishEndpoint
IBus
```

Trong bài này mình dùng:

```text
IPublishEndpoint
```

---

## 15. Publish OrderCreatedEvent trong CreateOrderHandler

Tìm handler tạo order.

Ví dụ:

```text
Services/OrderingService/Application/Orders/CreateOrder/CreateOrderHandler.cs
```

Inject thêm `IPublishEndpoint`.

```csharp
using MassTransit;
using MediatR;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Application.Orders;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Orders;
```

Ví dụ handler sau khi thêm publish:

```csharp
public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderDto>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPublishEndpoint _publishEndpoint;

    public CreateOrderHandler(
        IOrderRepository orderRepository,
        IPublishEndpoint publishEndpoint)
    {
        _orderRepository = orderRepository;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = new Order(
            Guid.NewGuid(),
            request.CustomerId,
            request.Items,
            DateTime.UtcNow);

        var createdOrder = await _orderRepository.CreateAsync(order, cancellationToken);

        var orderCreatedEvent = new OrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            OrderId = createdOrder.Id,
            CustomerId = createdOrder.CustomerId,
            TotalAmount = createdOrder.TotalAmount,
            Currency = "VND"
        };

        await _publishEndpoint.Publish(orderCreatedEvent, cancellationToken);

        return OrderMapper.ToDto(createdOrder);
    }
}
```

Quan trọng:

```text
Code trên là mẫu theo tên property phổ biến.
Project của bạn có thể dùng tên khác như TotalPrice/GrandTotal/OrderItems.
Map lại theo model hiện tại.
```

Nếu handler hiện tại đã có logic tạo order khác, **giữ logic cũ**, chỉ thêm:

```text
Inject IPublishEndpoint.
Sau khi create order thành công, tạo OrderCreatedEvent.
Gọi _publishEndpoint.Publish(...).
```

---

## 16. Tại sao không tự tạo IEventBus trong bài này?

Bản cũ dùng:

```text
IEventBus
RabbitMqEventBus
RabbitMQ.Client
```

Cách đó không sai về mặt học abstraction, nhưng lệch roadmap hiện tại vì Buổi 19 đã chọn:

```text
RabbitMQ + MassTransit
```

Trong MassTransit, abstraction đã có sẵn:

```text
IPublishEndpoint
IBus
IConsumer<T>
```

Vì vậy bài 19 dùng trực tiếp `IPublishEndpoint` cho dễ và đúng roadmap.

Sau này nếu muốn che MassTransit sau abstraction riêng, có thể làm ở Stage 2 hoặc khi refactor `BuildingBlocks.Messaging`.

---

## 17. Build và chạy

Build OrderingService:

```bash
dotnet build Services/OrderingService/OrderingService.csproj
```

Chạy RabbitMQ:

```bash
docker compose up -d rabbitmq
```

Chạy OrderingService:

```bash
dotnet run --project Services/OrderingService/OrderingService.csproj
```

Nếu tạo order cần BasketService hoặc service khác tùy flow, chạy thêm service tương ứng.

---

## 18. Kiểm tra MassTransit trong RabbitMQ UI

Mở RabbitMQ UI:

```text
http://localhost:15672
```

Login:

```text
microshop / microshop
```

Sau khi app chạy và publish message, RabbitMQ UI có thể xuất hiện exchange do MassTransit tạo.

MassTransit thường tạo exchange theo message type/convention. Vì bài này chưa có consumer, message publish có thể đi vào exchange nhưng chưa có queue nhận nếu chưa bind queue phù hợp.

Để quan sát dễ hơn trong bài học, ta tạo queue test và bind vào exchange event.

---

## 19. Tạo queue test để quan sát message

Có 2 cách quan sát.

### Cách A: Sau khi publish lần đầu rồi bind queue

1. Chạy OrderingService.
2. Tạo order bằng Postman để MassTransit publish event.
3. Vào RabbitMQ UI → Exchanges.
4. Tìm exchange liên quan tới `OrderCreatedEvent`.
5. Tạo queue:

```text
microshop.order-created.test
```

6. Bind queue vào exchange đó.
7. Tạo order lần nữa.
8. Queue sẽ nhận message.

### Cách B: Dùng consumer sau ở Buổi 21

Nếu không muốn bind thủ công hôm nay, chỉ cần thấy exchange/message publish không lỗi là đạt nền tảng.

Nhưng để bài 19 có output rõ ràng, khuyên dùng Cách A.

Lưu ý quan trọng:

```text
RabbitMQ message publish vào exchange.
Queue chỉ nhận được message nếu có binding phù hợp.
Nếu chưa có consumer/queue binding, publish không đồng nghĩa với queue có message ready.
```

Đây cũng là lý do Buổi 21 tạo NotificationWorker để có consumer endpoint rõ ràng.

---

## 20. Test bằng Postman

Tạo collection:

```text
MicroShop - Lesson 19 RabbitMQ MassTransit
```

Environment:

| Variable | Value |
| --- | --- |
| `ordering_url` | `http://localhost:5004` |
| `customer_id` | `11111111-1111-1111-1111-111111111111` |

### Test 1: Create Order

Request tùy endpoint hiện tại của bạn.

Ví dụ:

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

Expected:

```text
HTTP 201 Created hoặc 200 OK tùy endpoint hiện tại.
Có orderId.
OrderingService không lỗi khi publish event.
```

### Test 2: Kiểm tra RabbitMQ

Trong RabbitMQ UI:

```text
Exchanges
→ tìm exchange liên quan OrderCreatedEvent
```

Nếu đã bind queue test:

```text
Queues and Streams
→ microshop.order-created.test
→ Ready messages tăng lên
→ Get messages
```

Payload kỳ vọng có thông tin tương tự:

```json
{
  "eventId": "...",
  "occurredAtUtc": "...",
  "orderId": "...",
  "customerId": "11111111-1111-1111-1111-111111111111",
  "totalAmount": 1977.3,
  "currency": "VND"
}
```

Tùy MassTransit envelope/config, UI có thể thấy message được wrap thêm metadata. Điều quan trọng là payload có dữ liệu event.

---

## 21. Checklist hoàn thành bài

```text
[ ] RabbitMQ chạy bằng Docker Compose.
[ ] Mở được RabbitMQ Management UI.
[ ] Cài MassTransit.
[ ] Cài MassTransit.RabbitMQ.
[ ] OrderingService có config RabbitMq.
[ ] Có RabbitMqOptions.
[ ] Program.cs đăng ký AddMassTransit + UsingRabbitMq.
[ ] Có OrderCreatedEvent tạm trong OrderingService.
[ ] CreateOrderHandler inject IPublishEndpoint.
[ ] Sau khi tạo order, handler gọi Publish(OrderCreatedEvent).
[ ] Tạo order bằng Postman không lỗi.
[ ] RabbitMQ UI thấy exchange/message liên quan OrderCreatedEvent.
[ ] Nếu tạo queue test, queue nhận được message.
[ ] Bạn giải thích được vì sao bài này chưa tạo consumer.
[ ] Bạn giải thích được vì sao bài 20 mới tách BuildingBlocks.Contracts.
```

---

## 22. Bài tập

### Bài 1: Vẽ flow publish event

Vẽ lại flow:

```text
POST /orders
→ CreateOrderHandler
→ Save order
→ IPublishEndpoint.Publish(OrderCreatedEvent)
→ MassTransit
→ RabbitMQ
```

Giải thích từng phần:

```text
IPublishEndpoint là gì?
MassTransit làm gì?
RabbitMQ làm gì?
Exchange/queue khác nhau thế nào?
```

### Bài 2: Ghi lại message thực tế

Sau khi tạo order, vào RabbitMQ UI và ghi lại:

```text
Exchange name:
Queue test name nếu có:
Message count:
Payload có EventId không?
Payload có OrderId không?
Payload có CustomerId không?
```

### Bài 3: REST vs Event

Trả lời:

```text
Vì sao không nên gọi NotificationService trực tiếp trong CreateOrderHandler?
Khi nào REST vẫn phù hợp?
Khi nào event phù hợp?
```

### Bài 4: Production risk

Trả lời:

```text
Nếu order save DB thành công nhưng publish RabbitMQ fail thì chuyện gì xảy ra?
```

Gợi ý:

```text
Order đã tồn tại trong DB nhưng không có OrderCreatedEvent.
Worker/consumer phía sau không biết order đã tạo.
Đây là vấn đề Outbox sẽ giải quyết ở Buổi 23 và nâng sâu ở Buổi 38.
```

### Bài 5: Roadmap awareness

Giải thích:

```text
Buổi 19 khác Buổi 20 ở đâu?
Buổi 20 khác Buổi 21 ở đâu?
```

Đáp án gợi ý:

```text
Buổi 19: publish event bằng MassTransit.
Buổi 20: chuẩn hóa shared event contract trong BuildingBlocks.Contracts.
Buổi 21: tạo NotificationWorker consume OrderCreatedEvent.
```

---

## 23. Quiz nhanh

**Câu 1. Buổi 19 theo roadmap hiện tại học gì?**

```text
A. RabbitMQ + MassTransit
B. BuildingBlocks.Contracts + Event Design
C. NotificationWorker
D. Outbox
```

Đáp án: A

**Câu 2. Buổi 20 học gì?**

```text
A. RabbitMQ + MassTransit
B. BuildingBlocks.Contracts + Event Design
C. NotificationWorker
D. Saga
```

Đáp án: B

**Câu 3. MassTransit giúp gì trong bài này?**

```text
A. Thay database
B. Cung cấp programming model để publish message qua RabbitMQ dễ hơn
C. Tạo JWT
D. Chạy API Gateway
```

Đáp án: B

**Câu 4. OrderCreatedEvent là gì?**

```text
A. Command yêu cầu tạo order
B. Event thông báo order đã được tạo
C. Database migration
D. HTTP response
```

Đáp án: B

**Câu 5. Rủi ro production lớn nhất của bài 19 là gì?**

```text
A. Save order thành công nhưng publish event fail
B. CSS bị lỗi
C. Không có Swagger
D. Không có ProductMapper
```

Đáp án: A

---

## 24. Production mindset

Bài này chỉ là bước đầu tiên.

Flow hiện tại:

```text
Save order
→ Publish event bằng MassTransit
```

Rủi ro:

```text
Order save thành công nhưng publish event fail.
Publish event thành công nhưng sau này consumer xử lý fail.
Message có thể bị xử lý trùng.
Không có Outbox nên chưa đảm bảo event không mất.
Chưa có consumer nên chưa có end-to-end async flow hoàn chỉnh.
```

Các bài sau xử lý dần:

| Vấn đề | Bài học |
| --- | --- |
| Shared event contract | Buổi 20 |
| Consumer/worker | Buổi 21 |
| Retry/DLQ | Buổi 22 |
| Idempotency basic | Buổi 22 |
| Outbox basic | Buổi 23 |
| Transactional Outbox chuẩn | Buổi 38 |
| Inbox/WebhookLog | Buổi 39 |
| Saga | Buổi 43 |
| Webhook production handling | Buổi 44 |

Câu nhớ:

```text
MassTransit giúp publish/consume dễ hơn.
Outbox và idempotency mới giúp hệ event đáng tin hơn.
```

---

## 25. Lỗi hay gặp

| Lỗi | Nguyên nhân | Cách xử lý |
| --- | --- | --- |
| RabbitMQ UI không mở | Container chưa chạy hoặc port 15672 bị chiếm | `docker ps`, kiểm tra compose |
| Login RabbitMQ fail | Sai user/pass | Kiểm tra `RABBITMQ_DEFAULT_USER/PASS` |
| OrderingService không connect RabbitMQ | Sai Host/User/Pass hoặc RabbitMQ chưa chạy | Kiểm tra `RabbitMq` config |
| Build lỗi thiếu MassTransit namespace | Chưa cài package | Cài `MassTransit`, `MassTransit.RabbitMQ` |
| Không thấy queue có message | Publish vào exchange nhưng chưa có queue/binding hoặc chưa có consumer | Tạo/bind queue test hoặc chờ Buổi 21 |
| Publish fail làm API lỗi | RabbitMQ down hoặc config sai | Start RabbitMQ, kiểm tra logs |
| Message payload nhìn lạ | MassTransit có envelope/metadata | Tập trung kiểm tra event data |
| Không biết bind exchange nào | Publish một lần rồi vào Exchanges tìm exchange chứa tên event | Dùng RabbitMQ UI |

---

## 26. Điều kiện pass bài trong 90–120 phút

Bạn pass Buổi 19 khi:

```text
[ ] RabbitMQ chạy được.
[ ] OrderingService dùng MassTransit connect RabbitMQ.
[ ] Tạo order bằng Postman không lỗi.
[ ] CreateOrderHandler publish OrderCreatedEvent.
[ ] RabbitMQ UI thấy event/exchange/message.
[ ] Bạn giải thích được MassTransit khác RabbitMQ.Client ở mức cơ bản.
[ ] Bạn biết vì sao chưa làm consumer trong bài này.
[ ] Bạn biết bài 20 mới là BuildingBlocks.Contracts + Event Design.
```

Nếu hôm nay chỉ làm được:

```text
Order tạo thành công
MassTransit publish không lỗi
RabbitMQ UI thấy dấu hiệu message/exchange
```

là đủ pass bài.

---

## 27. Không làm trong bài này

Không làm:

```text
[ ] Không tạo BuildingBlocks.Contracts chính thức.
[ ] Không tách event contract shared sâu.
[ ] Không tạo NotificationWorker.
[ ] Không tạo consumer.
[ ] Không làm Retry/DLQ.
[ ] Không làm Idempotency.
[ ] Không làm Outbox.
[ ] Không làm Saga.
[ ] Không thêm Inventory/Shipping.
[ ] Không thay toàn bộ REST bằng event.
```

Lý do:

```text
Bài 19 chỉ học publish event đầu tiên bằng MassTransit.
Nếu nhồi consumer + retry + outbox sẽ quá tải và lệch roadmap.
```

---

## 28. Điều kiện mở khóa Buổi 20

Bạn có thể sang Buổi 20 khi:

```text
[ ] RabbitMQ chạy ổn.
[ ] OrderingService publish được OrderCreatedEvent bằng MassTransit.
[ ] Bạn hiểu event là chuyện đã xảy ra, không phải command.
[ ] Bạn hiểu vì sao cần chuẩn hóa event contract trước khi nhiều service cùng dùng.
```

Buổi 20 sẽ học đúng roadmap:

```text
BuildingBlocks.Contracts + Event Design
```

Mục tiêu Buổi 20:

```text
Tạo shared contracts cho event.
Chuẩn hóa OrderCreatedIntegrationEvent.
Hiểu event naming, event versioning, contract ownership.
Chuẩn bị cho Buổi 21 NotificationWorker consume event.
```
