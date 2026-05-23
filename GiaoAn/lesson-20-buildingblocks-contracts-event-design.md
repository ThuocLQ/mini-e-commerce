---
lesson: 20
title: "BuildingBlocks.Contracts + Event Design"
duration: "90–120 phút"
phase: "Phase 1.5 - RabbitMQ + Reliability Basic"
project: "MicroShop"
testing: "Postman-first"
type: "lesson"
roadmap_alignment: "Buổi 20 đúng roadmap: BuildingBlocks.Contracts + Event Design. Buổi 21 mới là NotificationWorker."
---

# Buổi 20: BuildingBlocks.Contracts + Event Design

## 0. Vị trí trong lộ trình

Theo lộ trình chuẩn Phase 1.5:

```text
Buổi 19: RabbitMQ + MassTransit
Buổi 20: BuildingBlocks.Contracts + Event Design
Buổi 21: NotificationWorker
Buổi 22: Retry/DLQ + Idempotency Basic
Buổi 23: Outbox Basic + Background Publisher Intro
```

Buổi 19 bạn đã mở cửa event-driven bằng cách:

```text
OrderingService
→ publish OrderCreatedEvent bằng MassTransit
→ RabbitMQ nhận message
```

Nhưng event ở bài 19 vẫn đang là bản tạm:

```text
OrderingService/Application/IntegrationEvents/OrderCreatedEvent.cs
```

Vấn đề:

```text
Nếu NotificationWorker ở Buổi 21 muốn consume event này, nó cần biết contract event.
Nếu mỗi service tự copy class event riêng, rất dễ lệch field, lệch tên, lệch version.
```

Vì vậy Buổi 20 tạo shared contract:

```text
BuildingBlocks.Contracts
```

Câu nhớ:

```text
Event contract là lời hứa giữa service publish và service consume.
```

---

## 1. Mục tiêu bài học

Trong 90–120 phút, mục tiêu là:

```text
[ ] Hiểu vì sao cần shared event contracts.
[ ] Tạo project BuildingBlocks.Contracts.
[ ] Tạo IntegrationEvent base contract.
[ ] Tạo OrderCreatedIntegrationEvent chuẩn hơn.
[ ] Refactor OrderingService dùng contract từ BuildingBlocks.Contracts.
[ ] Config MassTransit publish event contract mới.
[ ] Test lại bằng Postman.
[ ] Kiểm tra RabbitMQ thấy event mới.
[ ] Hiểu event naming/versioning ở mức cơ bản.
[ ] Chuẩn bị cho Buổi 21 NotificationWorker consume event.
```

Bài này chưa tạo worker consume event.

---

## 2. Vì sao không để event trong OrderingService?

Ở bài 19, để `OrderCreatedEvent` trong OrderingService là hợp lý vì mục tiêu là publish nhanh để hiểu RabbitMQ + MassTransit.

Nhưng khi sang consumer thật, event không còn là chuyện riêng của OrderingService nữa.

Ví dụ:

```text
OrderingService publish OrderCreatedEvent.
NotificationWorker consume OrderCreatedEvent.
ProjectionWorker sau này cũng có thể consume OrderCreatedEvent.
AuditWorker sau này cũng có thể consume OrderCreatedEvent.
```

Nếu event nằm trong OrderingService, các project khác có thể bị kéo phụ thuộc ngược vào service cụ thể:

```text
NotificationWorker → OrderingService
ProjectionWorker → OrderingService
```

Đây là coupling xấu.

Mục tiêu đúng hơn:

```text
OrderingService → BuildingBlocks.Contracts
NotificationWorker → BuildingBlocks.Contracts
ProjectionWorker → BuildingBlocks.Contracts
```

Shared contracts giúp:

```text
Một nơi định nghĩa event.
Publisher và consumer cùng dùng contract.
Giảm copy/paste class.
Dễ quản lý naming/versioning.
Chuẩn bị cho contract governance sau này.
```

---

## 3. Event contract là gì?

Event contract là cấu trúc dữ liệu được thỏa thuận giữa publisher và consumer.

Ví dụ:

```text
OrderCreatedIntegrationEvent
```

Nó trả lời:

```text
Event tên gì?
Có field nào?
Field nào bắt buộc?
Field có ý nghĩa gì?
Version hiện tại là gì?
Khi nào được publish?
Ai sở hữu event?
Consumer được phép kỳ vọng gì?
```

Event contract không nên là class ngẫu nhiên chỉ để serialize JSON.

Nó là một phần public contract giữa services.

---

## 4. Integration Event khác Domain Event thế nào?

Trong khóa này, nhớ đơn giản:

| Loại event | Phạm vi | Ví dụ | Ai dùng |
| --- | --- | --- | --- |
| Domain Event | Bên trong một bounded context/service | `OrderCreatedDomainEvent` | Nội bộ OrderingService |
| Integration Event | Giao tiếp giữa services | `OrderCreatedIntegrationEvent` | OrderingService, NotificationWorker, ProjectionWorker |

Bài này học Integration Event.

Câu nhớ:

```text
Domain Event là chuyện nội bộ domain.
Integration Event là chuyện service khác có thể biết.
```

Bài này chưa cần implement Domain Event pattern.

---

## 5. Naming rule cho event

Event nên đặt tên dạng quá khứ:

```text
OrderCreatedIntegrationEvent
PaymentSucceededIntegrationEvent
PaymentFailedIntegrationEvent
BasketCheckedOutIntegrationEvent
```

Không nên đặt kiểu command:

```text
CreateOrderEvent
ProcessPaymentEvent
SendEmailEvent
```

Vì:

```text
Command = yêu cầu ai đó làm việc.
Event = thông báo việc đã xảy ra.
```

Câu nhớ:

```text
Event nói "đã xảy ra", không nói "hãy làm".
```

---

## 6. Event field nên có gì?

Một integration event baseline nên có:

```text
EventId
OccurredAtUtc
Version
Business data
```

Ví dụ với `OrderCreatedIntegrationEvent`:

```text
EventId
OccurredAtUtc
Version
OrderId
CustomerId
TotalAmount
Currency
```

Giải thích:

| Field | Ý nghĩa |
| --- | --- |
| `EventId` | ID duy nhất của event, dùng cho idempotency sau này |
| `OccurredAtUtc` | Thời điểm sự kiện xảy ra |
| `Version` | Version contract |
| `OrderId` | Order vừa tạo |
| `CustomerId` | Customer sở hữu order |
| `TotalAmount` | Tổng tiền |
| `Currency` | Đơn vị tiền |

---

## 7. Versioning mindset

Bài này chỉ học versioning ở mức cơ bản.

Nguyên tắc:

```text
Không phá consumer cũ nếu chưa có kế hoạch.
```

Thêm field mới thường ít nguy hiểm hơn:

```text
V1:
OrderId, CustomerId, TotalAmount

V2:
OrderId, CustomerId, TotalAmount, Currency
```

Xóa/đổi tên field nguy hiểm hơn:

```text
Đổi TotalAmount thành Amount
Xóa CustomerId
Đổi kiểu dữ liệu decimal thành string
```

Cách an toàn:

```text
Thêm field mới.
Giữ field cũ một thời gian.
Tạo event version mới nếu thay đổi phá vỡ contract.
```

Bài này thêm property:

```text
Version = 1
```

Để sau này dễ nói về event evolution.

---

## 8. Target architecture sau bài này

Trước bài 20:

```text
OrderingService
└── OrderCreatedEvent nằm trong OrderingService
```

Sau bài 20:

```text
MicroShop/
├── BuildingBlocks/
│   └── BuildingBlocks.Contracts/
│       └── Events/
│           ├── IntegrationEvent.cs
│           └── Orders/
│               └── OrderCreatedIntegrationEvent.cs
└── Services/
    └── OrderingService/
        └── reference BuildingBlocks.Contracts
```

Flow:

```text
POST /orders
→ CreateOrderHandler
→ Save order
→ Publish OrderCreatedIntegrationEvent từ BuildingBlocks.Contracts
→ MassTransit
→ RabbitMQ
```

---

## 9. Tạo project BuildingBlocks.Contracts

Đứng ở root solution `MicroShop`.

Tạo folder nếu chưa có:

```bash
mkdir -p BuildingBlocks
```

Tạo class library:

```bash
dotnet new classlib -n BuildingBlocks.Contracts -o BuildingBlocks/BuildingBlocks.Contracts
```

Add vào solution:

```bash
dotnet sln add BuildingBlocks/BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj
```

Xóa file mẫu nếu có:

```text
Class1.cs
```

PowerShell:

```powershell
Remove-Item BuildingBlocks/BuildingBlocks.Contracts/Class1.cs
```

Build kiểm tra:

```bash
dotnet build BuildingBlocks/BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj
```

Pass khi:

```text
[ ] Có project BuildingBlocks.Contracts.
[ ] Project build pass.
[ ] Project đã được add vào solution.
```

---

## 10. Tạo folder structure cho contracts

Tạo folder:

```text
BuildingBlocks/BuildingBlocks.Contracts/Events
BuildingBlocks/BuildingBlocks.Contracts/Events/Orders
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force BuildingBlocks/BuildingBlocks.Contracts/Events
New-Item -ItemType Directory -Force BuildingBlocks/BuildingBlocks.Contracts/Events/Orders
```

Cấu trúc mục tiêu:

```text
BuildingBlocks.Contracts/
├── Events/
│   ├── IntegrationEvent.cs
│   └── Orders/
│       └── OrderCreatedIntegrationEvent.cs
└── BuildingBlocks.Contracts.csproj
```

---

## 11. Tạo IntegrationEvent base contract

Tạo file:

```text
BuildingBlocks/BuildingBlocks.Contracts/Events/IntegrationEvent.cs
```

```csharp
namespace BuildingBlocks.Contracts.Events;

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public int Version { get; init; } = 1;
}
```

Giải thích:

```text
EventId: dùng cho tracing/idempotency sau này.
OccurredAtUtc: dùng để biết event xảy ra lúc nào.
Version: dùng cho event contract evolution.
```

Vì sao dùng `abstract record`?

```text
IntegrationEvent là base contract.
Không publish trực tiếp IntegrationEvent.
Mỗi event cụ thể kế thừa nó.
```

---

## 12. Tạo OrderCreatedIntegrationEvent

Tạo file:

```text
BuildingBlocks/BuildingBlocks.Contracts/Events/Orders/OrderCreatedIntegrationEvent.cs
```

```csharp
using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Events.Orders;

public sealed record OrderCreatedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "VND";
}
```

Lưu ý:

```text
Không để logic xử lý order trong event contract.
Event contract chỉ nên chứa data cần thiết cho consumer.
```

Không nên thêm quá nhiều field lúc đầu:

```text
Toàn bộ order items
Thông tin payment
Thông tin inventory
Thông tin shipment
```

Bài này giữ contract tối thiểu.

---

## 13. Add reference từ OrderingService sang BuildingBlocks.Contracts

Chạy:

```bash
dotnet add Services/OrderingService/OrderingService.csproj reference BuildingBlocks/BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj
```

Build:

```bash
dotnet build Services/OrderingService/OrderingService.csproj
```

Pass khi:

```text
[ ] OrderingService reference được BuildingBlocks.Contracts.
[ ] Code compile pass trước khi refactor event.
```

---

## 14. Xóa hoặc ngưng dùng event tạm trong OrderingService

Ở Buổi 19 bạn có file:

```text
Services/OrderingService/Application/IntegrationEvents/OrderCreatedEvent.cs
```

Bây giờ có 2 lựa chọn:

### Cách khuyên dùng

Xóa file event tạm:

```powershell
Remove-Item Services/OrderingService/Application/IntegrationEvents/OrderCreatedEvent.cs
```

Vì event chuẩn đã nằm ở:

```text
BuildingBlocks.Contracts/Events/Orders/OrderCreatedIntegrationEvent.cs
```

### Cách an toàn nếu sợ lỗi

Giữ file cũ nhưng không dùng nữa.

Tuy nhiên dễ bị nhầm using, nên sau khi build pass nên xóa.

---

## 15. Refactor CreateOrderHandler dùng contract mới

Mở file handler tạo order, ví dụ:

```text
Services/OrderingService/Application/Orders/CreateOrder/CreateOrderHandler.cs
```

Thay using cũ:

```csharp
using OrderingService.Application.IntegrationEvents;
```

bằng:

```csharp
using BuildingBlocks.Contracts.Events.Orders;
using MassTransit;
```

Ví dụ publish event:

```csharp
var orderCreatedEvent = new OrderCreatedIntegrationEvent
{
    EventId = Guid.NewGuid(),
    OccurredAtUtc = DateTime.UtcNow,
    Version = 1,
    OrderId = createdOrder.Id,
    CustomerId = createdOrder.CustomerId,
    TotalAmount = createdOrder.TotalAmount,
    Currency = "VND"
};

await _publishEndpoint.Publish(orderCreatedEvent, cancellationToken);
```

Nếu property trong project bạn khác tên, map lại cho đúng:

```text
createdOrder.Id
createdOrder.CustomerId
createdOrder.TotalAmount hoặc TotalPrice/GrandTotal
```

Điểm cần nhớ:

```text
CreateOrderHandler không còn publish event class tự định nghĩa trong OrderingService.
Nó publish shared contract từ BuildingBlocks.Contracts.
```

---

## 16. Build toàn solution

Chạy:

```bash
dotnet build
```

Hoặc build riêng:

```bash
dotnet build BuildingBlocks/BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj
dotnet build Services/OrderingService/OrderingService.csproj
```

Nếu lỗi namespace:

```text
The type or namespace name 'BuildingBlocks' could not be found
```

Kiểm tra:

```text
[ ] OrderingService đã add project reference.
[ ] using đúng namespace BuildingBlocks.Contracts.Events.Orders.
[ ] File csproj reference đúng path.
```

---

## 17. Chạy RabbitMQ và OrderingService

Chạy RabbitMQ:

```bash
docker compose up -d rabbitmq
```

Chạy OrderingService:

```bash
dotnet run --project Services/OrderingService/OrderingService.csproj
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

## 18. Test bằng Postman

Tạo collection:

```text
MicroShop - Lesson 20 Event Contracts
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

Expected:

```text
HTTP 201 Created hoặc 200 OK tùy endpoint hiện tại.
Order được tạo thành công.
MassTransit publish OrderCreatedIntegrationEvent không lỗi.
```

---

## 19. Kiểm tra RabbitMQ UI

Sau khi tạo order, vào RabbitMQ UI:

```text
Exchanges
```

Bạn có thể thấy exchange liên quan tới:

```text
BuildingBlocks.Contracts.Events.Orders:OrderCreatedIntegrationEvent
```

hoặc tên tương tự theo convention của MassTransit.

Tùy version/config, tên exchange có thể khác. Tập trung kiểm tra:

```text
[ ] Có exchange/message type liên quan OrderCreatedIntegrationEvent.
[ ] Publish không lỗi.
[ ] Nếu có queue test bind đúng exchange, queue nhận được message.
```

Nếu dùng queue test từ bài 19, có thể cần bind lại vì message type/exchange đã đổi từ:

```text
OrderCreatedEvent
```

sang:

```text
OrderCreatedIntegrationEvent
```

Cách làm:

```text
1. Vào Exchanges.
2. Tìm exchange mới của OrderCreatedIntegrationEvent.
3. Tạo queue hoặc dùng queue test cũ.
4. Bind queue vào exchange mới.
5. Tạo order lần nữa.
6. Queue nhận message.
```

---

## 20. Checklist hoàn thành bài

```text
[ ] Tạo được project BuildingBlocks.Contracts.
[ ] Add BuildingBlocks.Contracts vào solution.
[ ] Xóa Class1.cs.
[ ] Tạo Events/IntegrationEvent.cs.
[ ] Tạo Events/Orders/OrderCreatedIntegrationEvent.cs.
[ ] Add reference từ OrderingService sang BuildingBlocks.Contracts.
[ ] CreateOrderHandler dùng OrderCreatedIntegrationEvent từ BuildingBlocks.Contracts.
[ ] Không còn dùng OrderCreatedEvent tạm trong OrderingService.
[ ] dotnet build pass.
[ ] Chạy RabbitMQ.
[ ] Chạy OrderingService.
[ ] Postman tạo order thành công.
[ ] MassTransit publish event contract mới không lỗi.
[ ] RabbitMQ UI thấy exchange/message liên quan OrderCreatedIntegrationEvent.
[ ] Hiểu event naming rule.
[ ] Hiểu versioning mindset cơ bản.
```

---

## 21. Bài tập

### Bài 1: Vẽ lại dependency sau bài 20

Vẽ:

```text
OrderingService
    ↓
BuildingBlocks.Contracts
    ↑
NotificationWorker sau này
```

Giải thích:

```text
Vì sao NotificationWorker không nên reference OrderingService?
Vì sao shared contract nằm ở BuildingBlocks.Contracts?
```

### Bài 2: So sánh event cũ và event mới

Ghi lại:

| Tiêu chí | Bài 19 | Bài 20 |
| --- | --- | --- |
| Event name | `OrderCreatedEvent` | `OrderCreatedIntegrationEvent` |
| Vị trí | Trong OrderingService | Trong BuildingBlocks.Contracts |
| Mục tiêu | Publish thử bằng MassTransit | Chuẩn hóa contract để service khác dùng |

### Bài 3: Event naming

Phân loại tên nào đúng/sai:

```text
OrderCreatedIntegrationEvent
CreateOrderIntegrationEvent
PaymentSucceededIntegrationEvent
SendEmailIntegrationEvent
BasketCheckedOutIntegrationEvent
```

Gợi ý:

```text
Tên đúng thường là sự kiện quá khứ: Created, Succeeded, Failed, CheckedOut.
Tên kiểu mệnh lệnh như Create/Send thường giống command hơn event.
```

### Bài 4: Versioning

Trả lời:

```text
Nếu sau này muốn thêm CustomerEmail vào OrderCreatedIntegrationEvent thì có phá consumer cũ không?
Nếu đổi TotalAmount thành Amount thì có nguy hiểm không?
Nếu xóa CustomerId thì sao?
```

Gợi ý:

```text
Thêm field thường ít nguy hiểm.
Đổi tên/xóa field thường là breaking change.
```

### Bài 5: Chuẩn bị cho Buổi 21

Ghi lại:

```text
NotificationWorker muốn consume OrderCreatedIntegrationEvent thì cần reference project nào?
Consumer cần biết event contract ở đâu?
```

Đáp án:

```text
Reference BuildingBlocks.Contracts.
Dùng OrderCreatedIntegrationEvent từ BuildingBlocks.Contracts.Events.Orders.
```

---

## 22. Quiz nhanh

**Câu 1. Vì sao cần BuildingBlocks.Contracts?**

```text
A. Để chứa toàn bộ business logic của mọi service
B. Để chứa shared contracts như integration events cho publisher/consumer dùng chung
C. Để thay thế database
D. Để chạy RabbitMQ container
```

Đáp án: B

**Câu 2. NotificationWorker có nên reference OrderingService để dùng event class không?**

```text
A. Có, vì OrderingService là publisher
B. Không, nên reference BuildingBlocks.Contracts để tránh coupling vào service cụ thể
C. Có, vì RabbitMQ yêu cầu vậy
D. Không, vì worker không dùng C#
```

Đáp án: B

**Câu 3. Event nên đặt tên như thế nào?**

```text
A. Dạng command, ví dụ CreateOrder
B. Dạng quá khứ, ví dụ OrderCreated
C. Dạng endpoint, ví dụ PostOrder
D. Dạng database, ví dụ InsertOrderRow
```

Đáp án: B

**Câu 4. Field EventId dùng để làm gì sau này?**

```text
A. Trang trí payload
B. Hỗ trợ tracing/idempotency, chống xử lý trùng event
C. Thay thế OrderId
D. Là password của event
```

Đáp án: B

**Câu 5. Buổi 21 sẽ học gì?**

```text
A. NotificationWorker consume OrderCreatedIntegrationEvent
B. Kafka Projection
C. Payment Saga
D. API Versioning
```

Đáp án: A

---

## 23. Production mindset

Bài 20 giúp project tiến gần production hơn ở điểm:

```text
Không để contract nằm lung tung trong từng service.
Không copy/paste event class giữa publisher và consumer.
Có event naming/versioning mindset.
Có nền tảng để nhiều worker/service consume cùng event.
```

Nhưng vẫn chưa production-safe hoàn toàn.

Chưa giải quyết:

```text
[ ] Retry.
[ ] DLQ.
[ ] Idempotent consumer.
[ ] Outbox.
[ ] Schema registry.
[ ] Contract testing.
[ ] Event compatibility automation.
```

Các phần này học sau:

| Chủ đề | Bài |
| --- | --- |
| NotificationWorker consume event | Buổi 21 |
| Retry/DLQ + Idempotency Basic | Buổi 22 |
| Outbox Basic | Buổi 23 |
| CloudEvents/Event Envelope | Buổi 37 |
| Transactional Outbox chuẩn | Buổi 38 |
| Inbox/WebhookLog | Buổi 39 |
| Schema Registry Concept | Buổi 62 optional |
| Contract Testing nâng cao | Buổi 63 optional |

---

## 24. Lỗi hay gặp

| Lỗi | Nguyên nhân | Cách xử lý |
| --- | --- | --- |
| `BuildingBlocks` namespace not found | Chưa add project reference | `dotnet add ... reference ...` |
| Vẫn publish event cũ | Chưa sửa using/handler | Dùng `BuildingBlocks.Contracts.Events.Orders` |
| RabbitMQ không thấy queue nhận message | Exchange đổi tên sau khi đổi contract | Bind queue test vào exchange mới |
| Build lỗi duplicate event type | Còn file event cũ trùng tên/using nhầm | Xóa event tạm hoặc đổi namespace rõ |
| Consumer sau này không deserialize được | Contract publisher/consumer lệch | Cùng reference BuildingBlocks.Contracts |
| Muốn thêm quá nhiều field vào event | Event contract bị phình | Giữ data tối thiểu, consumer cần gì thì cân nhắc kỹ |

---

## 25. Điều kiện pass bài trong 90–120 phút

Bạn pass Buổi 20 khi:

```text
[ ] BuildingBlocks.Contracts được tạo.
[ ] OrderCreatedIntegrationEvent nằm trong BuildingBlocks.Contracts.
[ ] OrderingService publish event contract mới.
[ ] Build pass.
[ ] Postman tạo order thành công.
[ ] RabbitMQ UI thấy event mới hoặc publish không lỗi.
[ ] Bạn giải thích được vì sao không để NotificationWorker reference OrderingService.
[ ] Bạn giải thích được event naming/versioning cơ bản.
```

Nếu hôm nay chỉ làm chắc việc:

```text
Tạo BuildingBlocks.Contracts
→ refactor OrderingService publish OrderCreatedIntegrationEvent
→ build/test pass
```

là đủ đạt mục tiêu bài 20.

---

## 26. Không làm trong bài này

Không làm:

```text
[ ] Không tạo NotificationWorker.
[ ] Không tạo consumer.
[ ] Không làm Retry/DLQ.
[ ] Không làm Idempotency.
[ ] Không làm Outbox.
[ ] Không làm CloudEvents envelope.
[ ] Không làm Schema Registry.
[ ] Không làm Contract Testing.
[ ] Không di chuyển toàn bộ BuildingBlocks.Logging/Messaging.
```

Lý do:

```text
Buổi 20 chỉ chuẩn hóa event contract.
Consumer và reliability để các bài tiếp theo.
```

---

## 27. Điều kiện mở khóa Buổi 21

Bạn có thể sang Buổi 21 khi:

```text
[ ] BuildingBlocks.Contracts tồn tại.
[ ] OrderCreatedIntegrationEvent là shared contract.
[ ] OrderingService publish contract mới bằng MassTransit.
[ ] Bạn hiểu publisher và consumer cần dùng chung event contract.
```

Buổi 21 sẽ học:

```text
NotificationWorker
```

Mục tiêu Buổi 21:

```text
Tạo worker service đầu tiên.
Reference BuildingBlocks.Contracts.
Consume OrderCreatedIntegrationEvent từ RabbitMQ.
Mô phỏng gửi notification/log message.
```
