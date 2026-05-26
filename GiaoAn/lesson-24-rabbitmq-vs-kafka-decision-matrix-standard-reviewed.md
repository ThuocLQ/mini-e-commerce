# Buổi 24: RabbitMQ vs Kafka - Decision Matrix cho MicroShop

## 0. Vị trí trong lộ trình

Bạn đã hoàn thành Phase 1.5:

```text
Buổi 19: RabbitMQ + MassTransit
Buổi 20: BuildingBlocks.Contracts + Event Design
Buổi 21: NotificationWorker
Buổi 22: Retry/DLQ + Idempotency Basic
Buổi 23: Outbox Basic + Background Publisher Intro
```

Sau bài 23, flow hiện tại của MicroShop là:

```text
POST /orders
→ OrderingService save Order + OutboxMessage
→ OutboxPublisher publish OrderCreatedIntegrationEvent
→ RabbitMQ
→ NotificationWorker consume event
→ retry / error queue / idempotency basic
```

Bài 24 mở Phase 1.6:

```text
Buổi 24: RabbitMQ vs Kafka
Buổi 25: Kafka Intro
Buổi 26: MongoDB Read Model
Buổi 27: Kafka → MongoDB Projection
```

Bài này chưa vội code Kafka.

Mục tiêu chính là hiểu:

```text
RabbitMQ dùng khi nào?
Kafka dùng khi nào?
Queue khác event stream ở đâu?
Vì sao không thay RabbitMQ bằng Kafka bừa bãi?
Trong MicroShop, flow nào nên dùng RabbitMQ, flow nào nên dùng Kafka?
```

Câu nhớ:

```text
RabbitMQ mạnh ở command/workflow queue.
Kafka mạnh ở event stream/history/replay/projection/analytics.
```

---

## 1. Mục tiêu bài học

Trong 90–120 phút, mục tiêu là:

```text
[ ] Hiểu RabbitMQ là message broker kiểu queue/routing.
[ ] Hiểu Kafka là distributed event log/event streaming platform.
[ ] Phân biệt queue và event stream.
[ ] Phân biệt competing consumers và consumer groups.
[ ] Biết khi nào dùng RabbitMQ cho MicroShop.
[ ] Biết khi nào dùng Kafka cho MicroShop.
[ ] Không thay RabbitMQ bằng Kafka chỉ vì Kafka "xịn" hơn.
[ ] Tạo decision matrix RabbitMQ vs Kafka.
[ ] Tạo ADR: chọn RabbitMQ cho workflow, Kafka cho projection/analytics.
[ ] Chuẩn bị mindset cho Buổi 25 Kafka Intro.
```

Output chính:

```text
docs/communication-decisions.md
docs/adr/ADR-002-rabbitmq-vs-kafka.md
```

Postman vẫn được dùng để verify flow hiện tại:

```text
POST /orders
GET /debug/outbox
RabbitMQ UI check queue
```

Bài này thiên về architecture decision.


---

## 1.1. Scope guard của bài 24

Bài 24 là bài **decision mindset**, không phải bài triển khai Kafka.

Trong bài này làm:

```text
[ ] Phân biệt RabbitMQ và Kafka.
[ ] Phân biệt queue và event stream.
[ ] Chọn broker theo use case của MicroShop.
[ ] Viết communication decision.
[ ] Viết ADR.
[ ] Verify lại flow RabbitMQ + Outbox hiện tại.
```

Không làm trong bài này:

```text
[ ] Không chạy Kafka container.
[ ] Không tạo Kafka topic.
[ ] Không viết Kafka producer.
[ ] Không viết Kafka consumer.
[ ] Không tạo MongoDB read model.
[ ] Không sửa OutboxPublisher để publish Kafka.
```

Lý do:

```text
Buổi 24 giúp chọn đúng công cụ.
Buổi 25 mới bắt đầu Kafka Intro bằng demo topic/partition/offset/consumer group.
```

Câu nhớ:

```text
Chưa hiểu RabbitMQ vs Kafka thì chưa nên code Kafka.
```

---

## 2. Vì sao cần học RabbitMQ vs Kafka?

Sau bài 23, bạn đã có RabbitMQ flow khá ổn:

```text
OrderingService
→ RabbitMQ
→ NotificationWorker
```

Nhưng roadmap sắp tới sẽ thêm Kafka:

```text
OrderCreatedIntegrationEvent
→ Kafka topic
→ ProjectionWorker
→ MongoDB OrderSummaryReadModel
```

Nếu không phân biệt rõ, rất dễ nghĩ:

```text
Đã có Kafka rồi thì bỏ RabbitMQ.
Kafka mạnh hơn RabbitMQ.
Cứ event là dùng Kafka.
Cứ async là dùng Kafka.
```

Đây là hiểu sai.

Thực tế:

```text
RabbitMQ và Kafka giải quyết các bài toán khác nhau.
Có hệ dùng cả hai.
Có hệ chỉ cần một trong hai.
Chọn sai sẽ làm hệ thống phức tạp không cần thiết.
```

Câu nhớ:

```text
Không có broker nào "tốt nhất cho mọi thứ".
Chỉ có broker phù hợp với use case.
```

---

## 3. RabbitMQ là gì trong mindset microservices?

RabbitMQ là message broker thiên về:

```text
Queue.
Routing.
Work distribution.
Task processing.
Workflow async.
```

Bạn publish message vào exchange. RabbitMQ route message vào queue. Consumer đọc message từ queue.

Flow đơn giản:

```text
Publisher
→ Exchange
→ Queue
→ Consumer
```

Trong MicroShop, RabbitMQ đang dùng cho:

```text
OrderCreatedIntegrationEvent
→ NotificationWorker gửi notification
```

Ý nghĩa nghiệp vụ:

```text
Khi order được tạo, cần có worker xử lý notification.
Message là một công việc cần xử lý.
Khi xử lý xong, message có thể được ack và biến mất khỏi queue.
```

Câu nhớ:

```text
RabbitMQ giống hàng đợi công việc cần xử lý.
```

---

## 4. Kafka là gì trong mindset microservices?

Kafka là distributed event log/event streaming platform.

Kafka lưu event vào topic. Topic có partitions. Consumer đọc event theo offset. Event thường được giữ lại một khoảng thời gian hoặc theo dung lượng retention.

Flow đơn giản:

```text
Producer
→ Topic partitions
→ Consumer group đọc theo offset
```

Điểm khác biệt lớn:

```text
Event không biến mất chỉ vì một consumer đọc xong.
Consumer group khác có thể đọc cùng event.
Consumer có thể replay từ offset cũ nếu retention còn.
```

Kafka phù hợp cho:

```text
Event stream.
Analytics.
Audit/event history.
Projection/read model.
Data pipeline.
Nhiều consumer độc lập cùng đọc event.
Replay/rebuild read model.
```

Trong MicroShop, Kafka sẽ dùng cho:

```text
Order events stream
→ ProjectionWorker
→ MongoDB OrderSummaryReadModel
```

Câu nhớ:

```text
Kafka giống nhật ký sự kiện có thể đọc lại.
```

---

## 5. Queue khác Event Stream thế nào?

### Queue mindset

```text
Message vào queue.
Một consumer trong nhóm xử lý message.
Xử lý xong thì ack.
Message thường biến mất khỏi queue.
```

Ví dụ:

```text
OrderCreated
→ NotificationWorker gửi email
```

Nếu có 3 instance NotificationWorker:

```text
Message A → Worker 1
Message B → Worker 2
Message C → Worker 3
```

Mục tiêu:

```text
Scale xử lý công việc.
Mỗi message chỉ cần một worker xử lý.
```

### Event Stream mindset

```text
Event được append vào log.
Consumer group đọc theo offset.
Nhiều consumer group khác nhau có thể đọc cùng event.
Event có thể replay nếu còn retention.
```

Ví dụ:

```text
OrderCreated
→ ProjectionWorker group đọc để build MongoDB read model
→ AnalyticsWorker group đọc để tính báo cáo
→ AuditWorker group đọc để ghi audit pipeline
```

Mục tiêu:

```text
Nhiều use case khác nhau cùng đọc lịch sử event.
Có khả năng replay/rebuild.
```

Câu nhớ:

```text
Queue hỏi: ai sẽ xử lý công việc này?
Stream hỏi: ai muốn quan sát sự kiện này?
```

---

## 6. So sánh RabbitMQ và Kafka

| Tiêu chí | RabbitMQ | Kafka |
| --- | --- | --- |
| Mô hình chính | Message broker / queue / routing | Distributed event log / stream |
| Message sau khi consume | Thường ack rồi biến mất khỏi queue | Vẫn còn trong log theo retention |
| Use case mạnh | Workflow, task queue, command async | Event streaming, projection, analytics |
| Routing | Rất mạnh với exchange/routing key | Chủ yếu theo topic/partition |
| Replay | Không phải điểm mạnh mặc định | Điểm mạnh nếu retention còn |
| Ordering | Theo queue trong phạm vi nhất định | Theo partition |
| Consumer scale | Competing consumers trên queue | Consumer group trên partitions |
| Nhiều consumer độc lập | Cần fanout/exchange/binding | Rất tự nhiên bằng nhiều consumer group |
| Message retry/DLQ | Rất phổ biến, dễ hiểu | Có nhưng pattern khác, thường dùng retry topic/DLT |
| Độ phức tạp vận hành | Thường đơn giản hơn | Phức tạp hơn |
| Phù hợp bài hiện tại | NotificationWorker | ProjectionWorker/AnalyticsWorker |

Không nên hiểu:

```text
Kafka thay thế RabbitMQ trong mọi case.
RabbitMQ yếu hơn Kafka.
Kafka là RabbitMQ bản lớn hơn.
```

Nên hiểu:

```text
RabbitMQ và Kafka có mental model khác nhau.
```

---

## 7. Competing Consumers vs Consumer Group

### RabbitMQ competing consumers

Một queue có nhiều consumer. RabbitMQ chia message cho các consumer.

```text
Queue: notification-order-created

Message 1 → Worker A
Message 2 → Worker B
Message 3 → Worker A
```

Mục tiêu:

```text
Tăng throughput cho cùng một loại công việc.
Một message thường chỉ một worker xử lý thành công.
```

### Kafka consumer group

Một topic có nhiều partition. Consumer trong cùng group chia partition với nhau.

```text
Topic: order-events
Partitions: P0, P1, P2

Group: projection-worker
Consumer A → P0
Consumer B → P1
Consumer C → P2
```

Consumer group khác đọc độc lập:

```text
Group: analytics-worker
Consumer X → P0, P1, P2
```

Mục tiêu:

```text
Mỗi group có offset riêng.
Nhiều group có thể đọc cùng event để phục vụ mục đích khác nhau.
```

Câu nhớ:

```text
RabbitMQ queue scale theo consumer cạnh tranh.
Kafka scale theo partition và consumer group.
```

---

## 8. Trong MicroShop, RabbitMQ dùng cho gì?

RabbitMQ nên dùng cho workflow nghiệp vụ cần xử lý async:

```text
OrderCreated → NotificationWorker gửi notification.
OrderCreated → Payment command nếu sau này dùng command async.
PaymentSucceeded → NotificationWorker gửi thông báo.
PaymentFailed → NotificationWorker gửi thông báo lỗi.
```

Đặc điểm các case này:

```text
Message đại diện cho công việc cần xử lý.
Một worker xử lý là đủ.
Cần retry/error queue.
Không nhất thiết cần replay lịch sử hàng tháng.
```

Ví dụ hiện tại:

```text
OrderCreatedIntegrationEvent
→ RabbitMQ
→ NotificationWorker
```

Nếu NotificationWorker fail:

```text
Retry.
Error queue.
Idempotency basic.
```

RabbitMQ fit vì:

```text
Notification là side effect/task.
Ta không cần nhiều consumer group độc lập đọc lại lịch sử notification.
```

---

## 9. Trong MicroShop, Kafka dùng cho gì?

Kafka nên dùng cho event stream cần nhiều consumer độc lập hoặc replay.

Các case trong MicroShop:

```text
OrderCreated
OrderPaid
OrderCancelled
PaymentSucceeded
PaymentFailed
```

Kafka dùng cho:

```text
ProjectionWorker:
    Build MongoDB read model cho order summary.

AnalyticsWorker:
    Tính số order theo ngày, doanh thu, conversion.

Audit/Event pipeline:
    Ghi lịch sử event phục vụ điều tra.

Search/Reporting:
    Đồng bộ sang read model/search index.
```

Đặc điểm:

```text
Event là fact đã xảy ra.
Nhiều consumer độc lập có thể quan tâm.
Có thể cần replay để rebuild read model.
Có thể cần giữ lịch sử event theo retention.
```

Ví dụ sắp học:

```text
OrderingService / Outbox Publisher
→ Kafka topic order-events
→ ProjectionWorker
→ MongoDB OrderSummaryReadModel
```

Câu nhớ:

```text
Kafka fit khi event có giá trị lâu dài hơn một task xử lý tức thời.
```

---

## 10. Một event có thể đi cả RabbitMQ và Kafka không?

Có, nhưng phải có lý do rõ.

Ví dụ `OrderCreatedIntegrationEvent` có thể:

```text
RabbitMQ:
    NotificationWorker gửi notification.

Kafka:
    ProjectionWorker build read model.
    AnalyticsWorker tính báo cáo.
```

Nhưng không nên publish lung tung.

Cần quyết định:

```text
Event này là task/workflow?
Event này là business fact cần stream?
Có bao nhiêu consumer độc lập?
Có cần replay không?
Có cần retention không?
Có cần ordering theo key không?
```

Trong MicroShop learning roadmap:

```text
RabbitMQ giữ vai trò workflow broker.
Kafka giữ vai trò event stream/projection broker.
```

Câu nhớ:

```text
Cùng tên event, nhưng mục đích publish sang từng broker phải rõ.
```

---

## 11. Decision Matrix cho MicroShop

| Câu hỏi | Nếu câu trả lời là Có | Nên nghiêng về |
| --- | --- | --- |
| Đây là task cần một worker xử lý? | Có | RabbitMQ |
| Cần retry/error queue đơn giản? | Có | RabbitMQ |
| Cần routing linh hoạt theo loại message? | Có | RabbitMQ |
| Cần nhiều consumer group độc lập đọc cùng event? | Có | Kafka |
| Cần replay/rebuild read model? | Có | Kafka |
| Cần lưu event history theo retention? | Có | Kafka |
| Cần stream analytics/reporting? | Có | Kafka |
| Cần ordering theo key trên event stream? | Có | Kafka |
| Team muốn vận hành đơn giản cho workflow nhỏ? | Có | RabbitMQ |
| Hệ thống cần event platform cho data pipeline? | Có | Kafka |

Kết luận cho MicroShop:

```text
Notification workflow:
    RabbitMQ.

Read model projection:
    Kafka.

Analytics:
    Kafka.

Simple async task:
    RabbitMQ.

Event stream nhiều consumer:
    Kafka.
```


### Decision table cụ thể cho MicroShop

| MicroShop event/use case | Broker đề xuất | Lý do |
| --- | --- | --- |
| `OrderCreatedIntegrationEvent` → gửi notification | RabbitMQ | Một worker xử lý side effect, cần retry/error queue |
| `PaymentSucceededIntegrationEvent` → gửi notification | RabbitMQ | Workflow/task async, không cần replay dài hạn |
| `OrderCreated` → build `OrderSummaryReadModel` | Kafka | Projection cần event stream và có thể replay |
| `OrderPaid` → analytics doanh thu | Kafka | Analytics consumer group độc lập |
| `OrderCancelled` → rebuild read model | Kafka | Cần lịch sử event theo retention |
| `SendEmailRequested` | RabbitMQ | Task queue, xử lý xong là đủ |
| `InventoryReserved` cho saga sau này | RabbitMQ hoặc Kafka tùy thiết kế | Nếu là command/workflow nghiêng RabbitMQ; nếu là fact stream nghiêng Kafka |

Lưu ý:

```text
Cùng một business fact có thể được publish sang cả RabbitMQ và Kafka,
nhưng mỗi broker phải có mục đích rõ.
```

---

## 12. Những hiểu nhầm phổ biến

### Hiểu nhầm 1: Kafka thay thế RabbitMQ

Sai.

Đúng hơn:

```text
Kafka có thể thay một số use case của RabbitMQ,
nhưng không phải replacement hoàn toàn.
```

Nếu bài toán là task queue đơn giản:

```text
Send email
Generate invoice
Process notification
```

RabbitMQ thường dễ vận hành và dễ hiểu hơn.

### Hiểu nhầm 2: RabbitMQ không làm event được

Sai.

RabbitMQ vẫn publish/consume event được.

Trong bài 19–23, bạn đã dùng RabbitMQ cho `OrderCreatedIntegrationEvent`.

Điểm khác là:

```text
RabbitMQ không được thiết kế như event log replay dài hạn như Kafka.
```

### Hiểu nhầm 3: Kafka luôn đảm bảo exactly-once cho mọi thứ

Sai.

Kafka có một số cơ chế exactly-once trong phạm vi nhất định, nhưng hệ thống end-to-end vẫn cần:

```text
Idempotent consumer.
Unique keys.
Transactional write phía consumer nếu cần.
Retry/DLT pattern.
Schema/versioning.
```

Trong MicroShop, ta vẫn học:

```text
Idempotency.
Outbox.
Inbox.
Projection rebuild.
```

### Hiểu nhầm 4: Có Kafka thì không cần Outbox

Sai.

Kafka cũng là broker bên ngoài database.

Vẫn có lỗi:

```text
DB save thành công
→ publish Kafka fail
→ event mất
```

Vì vậy Outbox vẫn có giá trị khi publish sang Kafka.

Câu nhớ:

```text
Outbox không phụ thuộc RabbitMQ hay Kafka.
Outbox xử lý khoảng cách giữa DB transaction và message broker.
```

---

## 12.1. Anti-patterns cần tránh trong MicroShop

### Anti-pattern 1: Dùng Kafka cho mọi async task

Không nên:

```text
SendEmailCommand
→ Kafka topic
→ EmailWorker
```

nếu bài toán chỉ là một task cần một worker xử lý.

Với case này, RabbitMQ thường phù hợp hơn:

```text
SendEmailRequested
→ RabbitMQ queue
→ EmailWorker
```

Lý do:

```text
Task queue cần retry/error queue đơn giản.
Không cần event history/replay dài hạn.
Không cần nhiều consumer group độc lập.
```

---

### Anti-pattern 2: Dùng RabbitMQ làm event history dài hạn

Không nên xem RabbitMQ queue như event store/read model source dài hạn.

Ví dụ không nên kỳ vọng:

```text
RabbitMQ giữ toàn bộ OrderCreated 6 tháng
→ ProjectionWorker replay lại từ đầu bất cứ lúc nào
```

RabbitMQ có thể giữ message nếu chưa ack, nhưng nó không phải event log replay platform như Kafka.

Nếu cần rebuild projection:

```text
Order events stream
→ Kafka
→ ProjectionWorker rebuild MongoDB read model
```

---

### Anti-pattern 3: Publish cùng một event lung tung sang mọi broker

Không nên:

```text
OrderCreated
→ RabbitMQ
→ Kafka
→ Redis Stream
→ Webhook
```

chỉ vì muốn “cho đủ công nghệ”.

Cần hỏi:

```text
Broker này phục vụ use case gì?
Ai consume?
Có cần replay không?
Có cần routing/task queue không?
Có cần retention không?
```

Nếu không trả lời được, chưa nên thêm broker.

---

### Anti-pattern 4: Nghĩ Kafka làm mất nhu cầu idempotency

Sai.

Kafka consumer vẫn có thể xử lý lại event do:

```text
Consumer restart.
Offset commit fail.
Rebalance.
Retry logic.
Manual replay.
```

Vì vậy ProjectionWorker sau này vẫn phải idempotent hoặc rebuild-safe.

Câu nhớ:

```text
Kafka giúp replay, nhưng replay cũng đồng nghĩa consumer phải chịu được xử lý lại.
```

---

## 13. Thiết kế dự kiến cho MicroShop sau Phase 1.6

Flow hiện tại:

```text
OrderingService
→ OutboxMessages
→ OutboxPublisher
→ RabbitMQ
→ NotificationWorker
```

Flow sắp tới:

```text
OrderingService
→ OutboxMessages
→ Publisher
    ├── RabbitMQ: notification/workflow
    └── Kafka: order-events stream

Kafka
→ ProjectionWorker
→ MongoDB OrderSummaryReadModel
```

Có thể tách publisher sau này:

```text
OutboxPublisherToRabbitMq
OutboxPublisherToKafka
```

Hoặc dùng event routing config:

```text
OrderCreated:
    RabbitMQ enabled
    Kafka enabled

PaymentSucceeded:
    RabbitMQ enabled
    Kafka enabled

InternalNotificationRequested:
    RabbitMQ only
```

Bài này chưa cần implement.

Chỉ cần hiểu design direction.

---

## 14. Postman Lab: verify RabbitMQ flow hiện tại

Bài này không code Kafka, nhưng vẫn verify flow RabbitMQ + Outbox đang ổn.

### Request 1: Create order

```text
POST {{ordering_url}}/orders
```

Body:

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
OutboxMessage created.
OutboxPublisher publish to RabbitMQ.
NotificationWorker consume event.
```

### Request 2: Check Outbox

```text
GET {{ordering_url}}/debug/outbox?limit=20
```

Expected:

```text
OrderCreatedIntegrationEvent status = Processed.
RetryCount = 0 nếu publish OK.
```

### Request 3: Check RabbitMQ UI

```text
http://localhost:15672
```

Expected:

```text
notification-order-created queue exists.
notification-order-created_error exists nếu bài 22 đã test failure.
```

Ghi chú:

```text
Nếu NotificationWorker đang chạy tốt, queue chính có thể về 0 rất nhanh.
```

---

## 15. Tạo docs communication decision

Tạo hoặc update file:

```text
docs/communication-decisions.md
```

Nội dung gợi ý:

```md
# MicroShop Communication Decisions

## 1. Synchronous Communication

### REST
Use REST for public/debug/simple request-response APIs.

### gRPC
Use gRPC for internal service-to-service calls where contract and performance matter.

## 2. Asynchronous Communication

### RabbitMQ

RabbitMQ is used for workflow/task-oriented asynchronous processing.

Use RabbitMQ when:
- A message should be handled by one worker.
- Retry/error queue behavior is important.
- The message represents a task or workflow side effect.
- Replay/history is not the main requirement.

MicroShop examples:
- OrderCreatedIntegrationEvent → NotificationWorker
- PaymentSucceededIntegrationEvent → NotificationWorker

### Kafka

Kafka is used for event streaming, projection and analytics.

Use Kafka when:
- Multiple independent consumer groups need the same event.
- Event history/replay matters.
- We need to rebuild read models.
- We need analytics/data pipeline.

MicroShop examples:
- Order events → ProjectionWorker → MongoDB read model
- Order events → AnalyticsWorker

## 3. Rule of Thumb

RabbitMQ answers:
Who should process this work?

Kafka answers:
Who wants to observe this event stream?

## 4. Outbox

Outbox is still needed when publishing to RabbitMQ or Kafka because the database and broker do not share the same transaction.

OrderingService saves business data and OutboxMessages in the same transaction.
Background publishers deliver events to brokers.
```

---

## 16. Tạo ADR cho RabbitMQ vs Kafka

Tạo folder nếu chưa có:

```text
docs/adr
```

Tạo file:

```text
docs/adr/ADR-002-rabbitmq-vs-kafka.md
```

Nội dung:

```md
# ADR-002: RabbitMQ vs Kafka Usage in MicroShop

## Status

Accepted for learning roadmap.

## Context

MicroShop uses asynchronous messaging for order-related workflows.
RabbitMQ is already used for OrderCreatedIntegrationEvent and NotificationWorker.
The roadmap will introduce Kafka for projection and analytics.

We need a clear decision to avoid replacing RabbitMQ with Kafka blindly.

## Decision

Use RabbitMQ for workflow/task-oriented messaging.

Use Kafka for event streaming, projection, analytics and replay-oriented use cases.

## RabbitMQ Use Cases

- NotificationWorker consumes order/payment notification messages.
- Simple async workflows.
- Retry/error queue scenarios.
- One message should usually be processed by one worker.

## Kafka Use Cases

- Order event stream.
- ProjectionWorker builds MongoDB read model.
- AnalyticsWorker consumes order events.
- Rebuild/replay read model scenarios.
- Multiple independent consumer groups.

## Consequences

Positive:
- Each broker is used for its strength.
- RabbitMQ remains simple for workflow processing.
- Kafka is introduced with a clear role.
- Future MongoDB projection has a proper event stream source.

Trade-offs:
- Running both RabbitMQ and Kafka increases operational complexity.
- Events may need routing rules.
- Outbox publishing may need separate publisher logic per broker.
- Consumers still need idempotency.

## Rule of Thumb

RabbitMQ:
Who should process this work?

Kafka:
Who wants to observe this event stream?
```

---

## 17. Bài tập

### Bài 1: Phân loại use case

Phân loại các case sau nên dùng RabbitMQ hay Kafka:

```text
1. Gửi notification khi order tạo thành công.
2. Build MongoDB OrderSummaryReadModel.
3. Tính doanh thu theo ngày.
4. Gửi email reset password.
5. Rebuild toàn bộ read model từ lịch sử order events.
6. Worker xử lý payment timeout.
```

Gợi ý:

```text
Task/workflow → RabbitMQ.
Stream/replay/analytics/projection → Kafka.
```

### Bài 2: Viết decision matrix ngắn

Tạo bảng:

```text
Use case
Broker
Lý do
```

Tối thiểu 5 dòng cho MicroShop.

### Bài 3: Review flow hiện tại

Dùng Postman:

```text
POST {{ordering_url}}/orders
GET {{ordering_url}}/debug/outbox?limit=20
```

Trả lời:

```text
Event hiện tại đang đi qua RabbitMQ để làm gì?
Nếu sau này thêm Kafka, event đó có thêm mục đích gì?
```

### Bài 4: Viết ADR

Tạo:

```text
docs/adr/ADR-002-rabbitmq-vs-kafka.md
```

Yêu cầu:

```text
[ ] Có Context.
[ ] Có Decision.
[ ] Có RabbitMQ Use Cases.
[ ] Có Kafka Use Cases.
[ ] Có Consequences.
[ ] Có Rule of Thumb.
```

### Bài 5: Interview answer

Trả lời bằng lời:

```text
RabbitMQ khác Kafka thế nào?
Khi nào dùng RabbitMQ?
Khi nào dùng Kafka?
Trong MicroShop vì sao dùng cả hai?
```

Câu trả lời nên có ý:

```text
RabbitMQ là broker cho queue/task/workflow.
Kafka là event log cho stream/replay/projection/analytics.
MicroShop dùng RabbitMQ cho NotificationWorker,
dùng Kafka cho ProjectionWorker/MongoDB read model và analytics.
```

---

## 18. Quiz nhanh

**Câu 1. RabbitMQ phù hợp nhất với case nào?**

```text
A. Gửi notification sau khi order được tạo
B. Rebuild MongoDB read model từ lịch sử event 6 tháng
C. Nhiều analytics consumer group đọc cùng event stream
D. Lưu event log dài hạn để replay
```

Đáp án: A

**Câu 2. Kafka phù hợp nhất với case nào?**

```text
A. Task gửi email đơn giản, xử lý xong thì ack
B. ProjectionWorker cần đọc order event stream để build read model
C. API Gateway routing HTTP request
D. Validate JWT token
```

Đáp án: B

**Câu 3. Vì sao không nên nói Kafka thay thế RabbitMQ hoàn toàn?**

```text
A. Vì Kafka và RabbitMQ có mental model/use case khác nhau
B. Vì Kafka không support message
C. Vì RabbitMQ bắt buộc phải dùng với .NET
D. Vì Kafka chỉ dùng cho frontend
```

Đáp án: A

**Câu 4. Outbox còn cần khi publish Kafka không?**

```text
A. Có, vì DB và Kafka vẫn là hai hệ thống transaction khác nhau
B. Không, Kafka tự đọc database
C. Không, Kafka đảm bảo mọi DB transaction
D. Chỉ cần nếu dùng RabbitMQ
```

Đáp án: A

**Câu 5. Rule of thumb nào đúng nhất?**

```text
A. RabbitMQ hỏi ai xử lý work này; Kafka hỏi ai muốn observe event stream này
B. RabbitMQ chỉ dùng cho frontend; Kafka chỉ dùng cho SQL
C. Kafka dùng cho mọi async; RabbitMQ chỉ dùng để học
D. RabbitMQ là database; Kafka là API Gateway
```

Đáp án: A

---

## 19. Production mindset

Sau bài này, bạn không chỉ biết dùng tool, mà biết chọn tool.

Điểm production-minded:

```text
Không chọn Kafka vì trend.
Không bỏ RabbitMQ vì Kafka nghe mạnh hơn.
Không dùng một broker cho mọi vấn đề nếu use case khác nhau rõ ràng.
Luôn hỏi event/message này dùng để xử lý task hay để stream/replay.
Vẫn cần Outbox khi publish sang bất kỳ broker nào.
Vẫn cần idempotency ở consumer.
```

MicroShop decision hiện tại:

```text
RabbitMQ:
    Workflow/task processing.
    NotificationWorker.
    Retry/error queue/idempotency basic.

Kafka:
    Event stream.
    ProjectionWorker.
    AnalyticsWorker.
    MongoDB read model rebuild.
```

---

## 19.1. Checkpoint tự vấn

Trước khi pass bài, tự trả lời nhanh:

```text
1. Nếu chỉ cần một worker gửi email, vì sao RabbitMQ fit hơn Kafka?
2. Nếu cần rebuild MongoDB read model, vì sao Kafka fit hơn RabbitMQ?
3. Nếu Kafka broker down lúc DB commit thành công, có còn cần Outbox không?
4. Nếu nhiều analytics service cùng muốn đọc OrderCreated, Kafka giải quyết tốt hơn ở điểm nào?
5. Nếu NotificationWorker tắt, RabbitMQ queue giữ message; vậy khác gì Kafka retention?
```

Gợi ý đáp án:

```text
1. Vì đây là task queue/workflow, không cần stream/replay dài hạn.
2. Vì Kafka lưu event log theo retention và consumer có offset để replay.
3. Có. Outbox vẫn cần vì DB và Kafka không chung transaction.
4. Mỗi consumer group có offset riêng, đọc cùng event độc lập.
5. RabbitMQ giữ message để chờ xử lý task; Kafka giữ event log cho nhiều group/replay.
```

---

## 20. Điều kiện pass bài

Bạn pass Buổi 24 khi:

```text
[ ] Giải thích được RabbitMQ khác Kafka thế nào.
[ ] Giải thích được queue khác event stream thế nào.
[ ] Biết RabbitMQ fit với NotificationWorker vì sao.
[ ] Biết Kafka fit với ProjectionWorker/MongoDB vì sao.
[ ] Không còn suy nghĩ Kafka là RabbitMQ bản xịn hơn.
[ ] Tạo được docs/communication-decisions.md.
[ ] Tạo được docs/adr/ADR-002-rabbitmq-vs-kafka.md.
[ ] Verify flow RabbitMQ + Outbox hiện tại vẫn chạy bằng Postman.
```

Nếu hôm nay bạn chỉ nhớ một câu:

```text
RabbitMQ xử lý công việc.
Kafka lưu và phát event stream.
```

là đã nắm được xương sống bài 24.

---

## 21. Điều kiện mở khóa Buổi 25

Bạn có thể sang Buổi 25 khi:

```text
[ ] Hiểu Kafka sẽ không thay RabbitMQ trong MicroShop.
[ ] Biết Kafka sẽ dùng cho event stream/projection/analytics.
[ ] Biết các khái niệm cần học tiếp: topic, partition, offset, consumer group.
[ ] Có ADR RabbitMQ vs Kafka.
```

Buổi 25 sẽ học:

```text
Kafka Intro - Topic / Partition / Offset / Consumer Group demo
```

Mục tiêu Buổi 25:

```text
Chạy Kafka local.
Tạo topic.
Produce event.
Consume event.
Hiểu offset và consumer group bằng demo.
Chuẩn bị cho Kafka → MongoDB Projection ở Buổi 27.
```
