# Buổi 25: Kafka Intro - Topic / Partition / Offset / Consumer Group Demo

## 0. Vị trí trong lộ trình

Bạn đã hoàn thành:

```text
Buổi 21: NotificationWorker
Buổi 22: Retry/DLQ + Idempotency Basic
Buổi 23: Outbox Basic + Background Publisher Intro
Buổi 24: RabbitMQ vs Kafka - Decision Matrix cho MicroShop
```

Sau bài 24, decision của MicroShop là:

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

Bài 25 bắt đầu Kafka ở mức căn bản.

Mục tiêu bài này:

```text
Chạy Kafka local.
Hiểu topic.
Hiểu partition.
Hiểu offset.
Hiểu consumer group.
Produce message vào topic.
Consume message từ topic.
Quan sát nhiều consumer group đọc cùng event độc lập.
```

Bài này chưa làm:

```text
[ ] Chưa tích hợp Kafka vào OrderingService.
[ ] Chưa sửa OutboxPublisher để publish Kafka.
[ ] Chưa tạo ProjectionWorker.
[ ] Chưa tạo MongoDB read model.
```

Các phần đó để bài sau:

```text
Buổi 26: MongoDB Read Model
Buổi 27: Kafka → MongoDB Projection
```

Câu nhớ:

```text
Buổi 24 chọn đúng công cụ.
Buổi 25 hiểu Kafka primitives.
Buổi 27 mới dùng Kafka để build projection.
```

---

## 1. Mục tiêu bài học

Trong 90–120 phút, mục tiêu là:

```text
[ ] Thêm Kafka + Zookeeper vào docker-compose.
[ ] Chạy Kafka local được.
[ ] Tạo topic microshop.order-events.
[ ] Produce message thủ công vào topic.
[ ] Consume message thủ công từ topic.
[ ] Hiểu topic là gì.
[ ] Hiểu partition là gì.
[ ] Hiểu offset là gì.
[ ] Hiểu consumer group là gì.
[ ] Test 2 consumer group đọc cùng event độc lập.
[ ] Test 2 consumer cùng group chia việc theo partition.
[ ] Ghi note Kafka vào docs.
```

Output chính:

```text
docker-compose.yml có Kafka/Zookeeper.
docs/kafka-intro-notes.md
```

Bài này dùng CLI để verify Kafka vì Kafka là infrastructure primitive.

Postman vẫn dùng để sanity check flow MicroShop hiện tại:

```text
POST {{ordering_url}}/orders
GET {{ordering_url}}/debug/outbox?limit=20
```

---

## 1.1. Scope guard

Bài này làm:

```text
[ ] Docker Compose Kafka local.
[ ] Kafka CLI produce/consume.
[ ] Topic/partition/offset/consumer group.
[ ] Consumer group lag.
[ ] Key-based partitioning mindset.
```

Bài này không làm:

```text
[ ] Không code .NET Kafka producer.
[ ] Không code .NET Kafka consumer.
[ ] Không publish Outbox sang Kafka.
[ ] Không tạo ProjectionWorker.
[ ] Không tạo MongoDB collection.
[ ] Không thay RabbitMQ bằng Kafka.
```

Lý do:

```text
Chưa hiểu Kafka primitives thì chưa nên nhảy vào code.
```


### Review note

Bài 25 được thiết kế theo hướng **Kafka primitives first**.

Điều quan trọng là sau bài này bạn phải tự giải thích được:

```text
Topic là dòng event gì?
Partition dùng để scale và giữ ordering kiểu gì?
Offset nằm ở đâu?
Consumer group khác nhau đọc độc lập ra sao?
Vì sao cùng group thì chia partition?
```

Nếu chỉ chạy được container Kafka nhưng chưa hiểu 4 khái niệm trên thì chưa nên sang Buổi 26.

---

## 2. Kafka trong MicroShop dùng để làm gì?

Trong MicroShop, Kafka không thay RabbitMQ.

Kafka sẽ được dùng cho:

```text
Order event stream.
ProjectionWorker.
AnalyticsWorker.
MongoDB read model rebuild.
```

Ví dụ tương lai:

```text
OrderingService
→ Kafka topic: microshop.order-events
→ ProjectionWorker
→ MongoDB OrderSummaryReadModel
```

Vì sao không dùng Kafka cho NotificationWorker hiện tại?

```text
Notification là task/workflow.
Một worker xử lý là đủ.
RabbitMQ retry/error queue dễ hơn cho bài toán này.
Không cần replay lịch sử notification dài hạn.
```

Vì sao Kafka fit với ProjectionWorker?

```text
Projection cần đọc event stream.
Có thể cần rebuild read model.
Nhiều consumer group khác nhau có thể đọc cùng event.
Kafka giữ event theo retention.
```

Câu nhớ:

```text
RabbitMQ xử lý work.
Kafka lưu event stream.
```

---

## 3. Kafka mental model

Kafka gồm vài khái niệm chính:

```text
Broker
Topic
Partition
Offset
Producer
Consumer
Consumer Group
```

Hình dung đơn giản:

```text
Producer
  |
  v
Topic: microshop.order-events
  |
  +-- Partition 0: offset 0, 1, 2, 3...
  |
  +-- Partition 1: offset 0, 1, 2, 3...
  |
  +-- Partition 2: offset 0, 1, 2, 3...
  |
  v
Consumer Group A
Consumer Group B
```

Kafka không chỉ là queue.

Kafka giống một log phân tán:

```text
Event được append vào topic partition.
Mỗi event có offset.
Consumer đọc theo offset.
Event không biến mất chỉ vì consumer đọc xong.
```

---

## 4. Topic là gì?

Topic là tên logical của một event stream.

Ví dụ:

```text
microshop.order-events
microshop.payment-events
microshop.catalog-events
```

Trong bài này dùng:

```text
microshop.order-events
```

Topic chứa các message/event cùng nhóm ý nghĩa.

Ví dụ event trong topic order-events:

```json
{
  "eventId": "11111111-1111-1111-1111-111111111111",
  "eventType": "OrderCreated",
  "orderId": "22222222-2222-2222-2222-222222222222",
  "customerId": "33333333-3333-3333-3333-333333333333",
  "totalAmount": 1977.3,
  "currency": "VND",
  "occurredAtUtc": "2026-05-27T10:00:00Z"
}
```

Câu nhớ:

```text
Topic là dòng sự kiện cùng chủ đề.
```

---

## 5. Partition là gì?

Một topic có thể chia thành nhiều partition.

Ví dụ:

```text
microshop.order-events
    partition 0
    partition 1
    partition 2
```

Partition giúp:

```text
Scale throughput.
Chia tải consumer.
Giữ ordering trong phạm vi một partition.
```

Điểm quan trọng:

```text
Kafka chỉ đảm bảo ordering trong cùng partition.
Không đảm bảo ordering toàn topic nếu topic có nhiều partition.
```

Ví dụ nếu key là `orderId`, Kafka có thể đưa tất cả event của cùng order vào cùng partition:

```text
Order 1001 Created
Order 1001 Paid
Order 1001 Cancelled
```

Như vậy ordering theo order được giữ tốt hơn.

Câu nhớ:

```text
Ordering trong Kafka là ordering theo partition, không phải toàn topic.
```

---

## 6. Offset là gì?

Offset là vị trí của message trong partition.

Ví dụ:

```text
Partition 0:
offset 0 → OrderCreated A
offset 1 → OrderCreated B
offset 2 → OrderPaid A
```

Consumer đọc message và lưu vị trí đã đọc.

Mỗi consumer group có offset riêng.

Ví dụ:

```text
Group projection-worker:
    đã đọc đến offset 100

Group analytics-worker:
    mới đọc đến offset 60
```

Hai group không ảnh hưởng nhau.

Câu nhớ:

```text
Offset là bookmark của consumer group trong partition.
```

---

## 7. Consumer Group là gì?

Consumer group là nhóm consumer cùng xử lý một stream với cùng mục đích.

Ví dụ:

```text
Group: projection-worker
    Consumer A
    Consumer B

Group: analytics-worker
    Consumer X
```

Trong cùng một group:

```text
Một partition chỉ được assigned cho một consumer tại một thời điểm.
```

Nếu topic có 3 partition và group có 3 consumer:

```text
Consumer A → partition 0
Consumer B → partition 1
Consumer C → partition 2
```

Nếu group có nhiều consumer hơn partition:

```text
Topic có 3 partition
Group có 5 consumer
→ 2 consumer sẽ idle
```

Consumer group khác có offset riêng:

```text
ProjectionWorker group đọc để build MongoDB.
AnalyticsWorker group đọc để tính report.
Cả hai đều có thể đọc cùng event stream.
```

Câu nhớ:

```text
Consumer trong cùng group chia việc.
Consumer khác group đọc độc lập.
```

---

## 8. Thêm Kafka vào docker-compose

Mở file:

```text
docker-compose.yml
```

Thêm services sau nếu chưa có:

```yaml
services:
  zookeeper:
    image: confluentinc/cp-zookeeper:7.6.1
    container_name: microshop-zookeeper
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
    ports:
      - "2181:2181"

  kafka:
    image: confluentinc/cp-kafka:7.6.1
    container_name: microshop-kafka
    depends_on:
      - zookeeper
    ports:
      - "9092:9092"
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181

      KAFKA_LISTENERS: PLAINTEXT://0.0.0.0:29092,PLAINTEXT_HOST://0.0.0.0:9092
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:29092,PLAINTEXT_HOST://localhost:9092
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT
      KAFKA_INTER_BROKER_LISTENER_NAME: PLAINTEXT

      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 1
      KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 1
```

Nếu file đã có `rabbitmq`, giữ nguyên.

Lưu ý:

```text
localhost:9092 dùng cho app/CLI chạy ngoài Docker trên máy host.
kafka:29092 dùng cho container khác trong cùng docker network.
```

Câu nhớ:

```text
Advertised listeners sai là lỗi Kafka local rất phổ biến.
```

---

## 9. Chạy Kafka local

Chạy:

```bash
docker compose up -d zookeeper kafka
```


Nếu muốn chạy kèm RabbitMQ luôn:

```bash
docker compose up -d rabbitmq zookeeper kafka
```

Trên Windows PowerShell cũng dùng y hệt:

```powershell
docker compose up -d zookeeper kafka
```

Kiểm tra containers:

```bash
docker ps
```

Expected:

```text
microshop-zookeeper
microshop-kafka
```

Xem logs Kafka:

```bash
docker logs microshop-kafka --tail 100
```

Nếu thấy Kafka start xong, tiếp tục tạo topic.

---

## 10. Tạo topic microshop.order-events

Chạy lệnh trong Kafka container:

```bash
docker exec -it microshop-kafka kafka-topics \
  --bootstrap-server localhost:9092 \
  --create \
  --if-not-exists \
  --topic microshop.order-events \
  --partitions 3 \
  --replication-factor 1
```

Nếu dùng PowerShell và bị lỗi do dấu `\`, dùng bản một dòng:

```powershell
docker exec -it microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
```

Lưu ý:

```text
--if-not-exists giúp chạy lại lệnh không bị fail nếu topic đã tồn tại.
```

List topics:

```bash
docker exec -it microshop-kafka kafka-topics \
  --bootstrap-server localhost:9092 \
  --list
```

Describe topic:

```bash
docker exec -it microshop-kafka kafka-topics \
  --bootstrap-server localhost:9092 \
  --describe \
  --topic microshop.order-events
```

Expected:

```text
Topic: microshop.order-events
PartitionCount: 3
ReplicationFactor: 1
```

Lưu ý:

```text
ReplicationFactor = 1 chỉ dùng local learning.
Production cần nhiều broker và replication factor lớn hơn.
```

---

## 11. Produce message thủ công

Mở terminal producer:

```bash
docker exec -it microshop-kafka kafka-console-producer \
  --bootstrap-server localhost:9092 \
  --topic microshop.order-events
```


PowerShell một dòng:

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events
```

Paste từng dòng JSON:

```json
{"eventId":"11111111-1111-1111-1111-111111111111","eventType":"OrderCreated","orderId":"ORD-001","customerId":"CUST-001","totalAmount":1977.3,"currency":"VND","occurredAtUtc":"2026-05-27T10:00:00Z"}
```

```json
{"eventId":"22222222-2222-2222-2222-222222222222","eventType":"OrderCreated","orderId":"ORD-002","customerId":"CUST-002","totalAmount":299.9,"currency":"VND","occurredAtUtc":"2026-05-27T10:01:00Z"}
```

```json
{"eventId":"33333333-3333-3333-3333-333333333333","eventType":"OrderPaid","orderId":"ORD-001","customerId":"CUST-001","totalAmount":1977.3,"currency":"VND","occurredAtUtc":"2026-05-27T10:02:00Z"}
```

Nhấn `Ctrl+C` để thoát producer.

Câu nhớ:

```text
Console producer là cách nhanh nhất để hiểu Kafka trước khi code .NET.
```

---

## 12. Consume message từ beginning

Mở terminal consumer:

```bash
docker exec -it microshop-kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic microshop.order-events \
  --from-beginning \
  --property print.offset=true \
  --property print.partition=true
```

PowerShell một dòng:

```powershell
docker exec -it microshop-kafka kafka-console-consumer --bootstrap-server localhost:9092 --topic microshop.order-events --from-beginning --property print.offset=true --property print.partition=true
```

Expected:

```text
Bạn thấy các JSON vừa produce, kèm partition/offset.
```

Ví dụ output có thể tương tự:

```text
Partition:0	Offset:0	{...}
Partition:1	Offset:0	{...}
```

Thoát bằng:

```text
Ctrl+C
```

---

## 13. Test consumer group độc lập

Consumer group A:

```bash
docker exec -it microshop-kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic microshop.order-events \
  --group projection-worker \
  --from-beginning
```

Consumer group B:

```bash
docker exec -it microshop-kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic microshop.order-events \
  --group analytics-worker \
  --from-beginning
```

Expected:

```text
Cả projection-worker và analytics-worker đều đọc được cùng các event.
```

Nếu không thấy event cũ dù có `--from-beginning`, nguyên nhân thường là:

```text
Group đó đã từng đọc và commit offset trước đó.
```

Cách test sạch:

```text
Đổi group name mới, ví dụ projection-worker-v2 hoặc analytics-worker-v2.
```

Vì:

```text
Mỗi consumer group có offset riêng.
```

Câu nhớ:

```text
Kafka cho nhiều consumer group đọc cùng event stream độc lập.
```

---

## 14. Test consumer cùng group chia việc

Mở 2 terminal cùng group:

Terminal 1:

```bash
docker exec -it microshop-kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic microshop.order-events \
  --group projection-worker-demo
```

Terminal 2:

```bash
docker exec -it microshop-kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic microshop.order-events \
  --group projection-worker-demo
```

Mở producer:

```bash
docker exec -it microshop-kafka kafka-console-producer \
  --bootstrap-server localhost:9092 \
  --topic microshop.order-events
```

Gửi nhiều dòng:

```json
{"eventId":"44444444-4444-4444-4444-444444444444","eventType":"OrderCreated","orderId":"ORD-003","customerId":"CUST-003","totalAmount":100,"currency":"VND","occurredAtUtc":"2026-05-27T10:03:00Z"}
```

```json
{"eventId":"55555555-5555-5555-5555-555555555555","eventType":"OrderCreated","orderId":"ORD-004","customerId":"CUST-004","totalAmount":200,"currency":"VND","occurredAtUtc":"2026-05-27T10:04:00Z"}
```

```json
{"eventId":"66666666-6666-6666-6666-666666666666","eventType":"OrderCreated","orderId":"ORD-005","customerId":"CUST-005","totalAmount":300,"currency":"VND","occurredAtUtc":"2026-05-27T10:05:00Z"}
```

Expected:

```text
Hai consumer cùng group chia nhau message theo partition assignment.
Không phải cả hai đều nhận toàn bộ message.
```

Lưu ý:

```text
Nếu message ít hoặc partition assignment chưa đều, có thể một consumer nhận nhiều hơn.
Điểm cần hiểu là cùng group thì chia việc, khác group thì đọc độc lập.
```

---

## 15. Xem consumer group offsets

List consumer groups:

```bash
docker exec -it microshop-kafka kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --list
```

Describe group:

```bash
docker exec -it microshop-kafka kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --describe \
  --group projection-worker
```

Bạn sẽ thấy:

```text
TOPIC
PARTITION
CURRENT-OFFSET
LOG-END-OFFSET
LAG
CONSUMER-ID
HOST
CLIENT-ID
```

Ý nghĩa:

| Field | Ý nghĩa |
| --- | --- |
| `CURRENT-OFFSET` | Offset group đã consume/commit |
| `LOG-END-OFFSET` | Offset mới nhất của partition |
| `LAG` | Số message group còn chưa đọc |
| `PARTITION` | Partition được theo dõi |

Câu nhớ:

```text
Lag cao nghĩa là consumer group đang đọc chậm hơn tốc độ event sinh ra.
```

---

## 16. Produce message có key

Kafka partitioning thường dùng key.

Nếu key giống nhau, message thường vào cùng partition.

Dùng producer có parse key:

```bash
docker exec -it microshop-kafka kafka-console-producer \
  --bootstrap-server localhost:9092 \
  --topic microshop.order-events \
  --property parse.key=true \
  --property key.separator=:
```

Gửi:

```text
ORD-100:{"eventId":"77777777-7777-7777-7777-777777777777","eventType":"OrderCreated","orderId":"ORD-100","customerId":"CUST-100","totalAmount":100,"currency":"VND","occurredAtUtc":"2026-05-27T10:10:00Z"}
```

```text
ORD-100:{"eventId":"88888888-8888-8888-8888-888888888888","eventType":"OrderPaid","orderId":"ORD-100","customerId":"CUST-100","totalAmount":100,"currency":"VND","occurredAtUtc":"2026-05-27T10:11:00Z"}
```

```text
ORD-200:{"eventId":"99999999-9999-9999-9999-999999999999","eventType":"OrderCreated","orderId":"ORD-200","customerId":"CUST-200","totalAmount":200,"currency":"VND","occurredAtUtc":"2026-05-27T10:12:00Z"}
```

Ý nghĩa:

```text
Key ORD-100 giúp các event của cùng order có khả năng vào cùng partition.
Ordering theo order ổn định hơn.
```

Trong .NET producer sau này, ta cũng sẽ cân nhắc:

```text
Key = OrderId
```

Để quan sát key khi consume, mở consumer với formatter:

```bash
docker exec -it microshop-kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic microshop.order-events \
  --from-beginning \
  --property print.key=true \
  --property key.separator=" | " \
  --property print.partition=true \
  --property print.offset=true
```

PowerShell một dòng:

```powershell
docker exec -it microshop-kafka kafka-console-consumer --bootstrap-server localhost:9092 --topic microshop.order-events --from-beginning --property print.key=true --property key.separator=" | " --property print.partition=true --property print.offset=true
```

Expected mindset:

```text
Các message có cùng key ORD-100 thường vào cùng partition.
Nhờ vậy event của cùng order giữ được thứ tự tương đối trong partition đó.
```

Câu nhớ:

```text
Chọn key sai có thể làm ordering và distribution không như mong muốn.
```

---

## 16.1. Những điểm dễ hiểu sai khi quan sát demo

### 1. `--from-beginning` không phải lúc nào cũng đọc lại từ đầu

Nếu consumer không có `--group`, mỗi lần chạy có thể đọc từ beginning theo command.

Nhưng nếu có `--group`, Kafka dùng committed offset của group.

Vì vậy:

```text
Group mới + --from-beginning → đọc từ đầu.
Group cũ đã commit offset → tiếp tục từ offset đã commit.
```

Muốn test lại sạch:

```text
Đổi group name mới.
```

---

### 2. Cùng group không có nghĩa là mọi consumer đều nhận mọi message

Cùng group nghĩa là chia partition.

```text
Consumer A nhận partition 0.
Consumer B nhận partition 1.
Consumer C nhận partition 2.
```

Nếu muốn mỗi service đều nhận cùng event, dùng group khác nhau:

```text
projection-worker group
analytics-worker group
```

---

### 3. Message ít quá thì nhìn phân phối không rõ

Nếu chỉ produce 2–3 message, có thể bạn thấy một consumer nhận hết.

Để quan sát rõ hơn:

```text
Produce nhiều message hơn.
Dùng topic có nhiều partition.
Dùng key khác nhau.
Describe consumer group để xem partition assignment.
```

---

### 4. Kafka giữ event theo retention, không giữ mãi mãi mặc định

Kafka cho replay trong phạm vi retention.

Không nên hiểu:

```text
Kafka luôn giữ event vĩnh viễn.
```

Production cần cấu hình retention theo nhu cầu:

```text
retention.ms
retention.bytes
cleanup.policy
```

---

## 17. Kafka local troubleshooting

| Lỗi | Nguyên nhân thường gặp | Cách xử lý |
| --- | --- | --- |
| Không connect được localhost:9092 | advertised listeners sai hoặc Kafka chưa ready | Check `KAFKA_ADVERTISED_LISTENERS`, xem logs |
| Topic create fail | Kafka chưa start xong | Đợi thêm, xem `docker logs microshop-kafka` |
| Consumer không thấy message | Sai topic hoặc consumer đã đọc offset rồi | Dùng group mới hoặc `--from-beginning` |
| Group không đọc lại from beginning | Group đã có committed offset | Dùng group name mới |
| Consumer cùng group không chia đều | Ít partition/message hoặc assignment chưa đều | Tạo nhiều message, kiểm tra partition count |
| Container port conflict | 9092 hoặc 2181 đã dùng | Đổi port hoặc tắt service đang chiếm |
| Docker pull chậm | Image confluent khá lớn | Chờ pull xong hoặc kiểm tra mạng |
| PowerShell báo lỗi với lệnh nhiều dòng | Dấu `\` là line continuation của bash, không phải PowerShell | Dùng bản một dòng hoặc dùng backtick của PowerShell |
| `--from-beginning` không đọc lại message cũ | Consumer group đã có committed offset | Dùng group name mới hoặc reset offset khi học nâng cao |

Lưu ý:

```text
Kafka local lỗi nhiều nhất ở advertised listeners.
```

---

## 18. Tạo docs kafka intro notes

Tạo file:

```text
docs/kafka-intro-notes.md
```

Nội dung gợi ý:

```md
# Kafka Intro Notes

## Topic

Topic is a named event stream.

MicroShop example:

```text
microshop.order-events
```

## Partition

A topic is split into partitions.

Kafka guarantees ordering inside a partition, not across the whole topic.

For order events, using `OrderId` as key helps keep events of the same order in the same partition.

## Offset

Offset is the position of a message in a partition.

Each consumer group tracks its own offset.

## Consumer Group

Consumers in the same group share partitions.

Different consumer groups read independently.

MicroShop examples:

```text
projection-worker group
analytics-worker group
```

## RabbitMQ vs Kafka reminder

RabbitMQ is for workflow/task processing.

Kafka is for event stream/projection/analytics/replay.

## Next

Buổi 26 will design MongoDB read model.

Buổi 27 will consume Kafka order events and build MongoDB projection.
```

---

## 19. Postman sanity check cho flow hiện tại

Bài này không thay đổi OrderingService.

Nhưng nên test lại flow cũ không bị ảnh hưởng.

Chạy:

```bash
docker compose up -d rabbitmq zookeeper kafka
dotnet run --project Services/OrderingService/OrderingService.csproj
dotnet run --project Services/NotificationWorker/NotificationWorker.csproj
```

Postman:

```text
POST {{ordering_url}}/orders
```

Check:

```text
GET {{ordering_url}}/debug/outbox?limit=20
```

Expected:

```text
Order tạo thành công.
Outbox RabbitMQ vẫn processed.
NotificationWorker vẫn nhận event.
Kafka chưa liên quan đến flow này.
```

Câu nhớ:

```text
Thêm Kafka vào infra không có nghĩa là flow RabbitMQ hiện tại bị thay thế.
```

---

## 20. Bài tập

### Bài 1: Giải thích topic/partition/offset/group

Viết ngắn:

```text
Topic là gì?
Partition là gì?
Offset là gì?
Consumer group là gì?
```

Yêu cầu dùng ví dụ:

```text
microshop.order-events
projection-worker
analytics-worker
```

---

### Bài 2: Test 2 consumer group

Tạo 2 group:

```text
projection-worker
analytics-worker
```

Produce 3 event vào `microshop.order-events`.

Ghi lại:

```text
Cả 2 group có đọc được cùng event không?
Vì sao?
```

---

### Bài 3: Test cùng group chia việc

Chạy 2 consumer cùng group:

```text
projection-worker-demo
```

Produce nhiều event.

Ghi lại:

```text
Hai consumer có cùng nhận toàn bộ event không?
Hay chia nhau?
Điều này khác gì với 2 group khác nhau?
```

---

### Bài 4: Xem lag

Dùng:

```bash
docker exec -it microshop-kafka kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --describe \
  --group projection-worker
```

Ghi lại:

```text
CURRENT-OFFSET là gì?
LOG-END-OFFSET là gì?
LAG là gì?
```

---

### Bài 5: Chọn key

Trả lời:

```text
Vì sao order event nên dùng OrderId làm key?
Nếu dùng random key thì điều gì có thể xảy ra?
```

Gợi ý:

```text
OrderId giúp event cùng order vào cùng partition.
Random key có thể làm event cùng order nằm rải rác nhiều partition, ordering theo order khó đảm bảo.
```

---

## 21. Quiz nhanh

**Câu 1. Kafka topic là gì?**

```text
A. Một named event stream
B. Một RabbitMQ queue
C. Một SQL table
D. Một HTTP endpoint
```

Đáp án: A

**Câu 2. Kafka đảm bảo ordering ở phạm vi nào?**

```text
A. Trong cùng partition
B. Toàn bộ cluster
C. Toàn bộ topic dù có nhiều partition
D. Toàn bộ consumer group khác nhau
```

Đáp án: A

**Câu 3. Offset là gì?**

```text
A. Vị trí của message trong partition
B. Port Kafka đang chạy
C. Tên topic
D. Số lượng broker trong cluster
```

Đáp án: A

**Câu 4. Hai consumer group khác nhau đọc cùng topic thì sao?**

```text
A. Mỗi group có offset riêng và có thể đọc cùng event độc lập
B. Group thứ hai không đọc được vì group thứ nhất đã đọc rồi
C. Kafka xóa message sau group đầu tiên đọc
D. RabbitMQ sẽ quyết định group nào đọc
```

Đáp án: A

**Câu 5. Vì sao chọn OrderId làm key cho order events?**

```text
A. Để event cùng order có khả năng vào cùng partition và giữ ordering theo order
B. Để Kafka tự tạo Order trong database
C. Để consumer không cần deserialize JSON
D. Để RabbitMQ không cần queue
```

Đáp án: A

---

## 22. Production mindset

Bài 25 mới là Kafka intro, nhưng cần nhớ vài điểm production:

```text
Replication factor = 1 chỉ dùng local.
Production cần nhiều broker.
Partition count cần tính theo throughput và consumer scale.
Key ảnh hưởng ordering và distribution.
Consumer group lag là chỉ số vận hành rất quan trọng.
Kafka không loại bỏ nhu cầu idempotency.
Kafka không loại bỏ nhu cầu Outbox.
```

MicroShop hiện tại:

```text
RabbitMQ vẫn xử lý NotificationWorker.
Kafka mới được setup để chuẩn bị event stream.
```

Tương lai:

```text
OrderingService event
→ Kafka topic microshop.order-events
→ ProjectionWorker
→ MongoDB read model
```

Câu nhớ:

```text
Kafka mạnh vì stream, offset, consumer group và replay.
Nhưng càng mạnh thì càng phải hiểu đúng cách vận hành.
```

---

## 22.1. Checkpoint tự vấn

Trước khi pass bài, tự trả lời:

```text
1. Topic khác queue ở điểm nào?
2. Vì sao Kafka ordering chỉ đảm bảo trong partition?
3. Offset thuộc về message hay consumer group?
4. Vì sao 2 consumer group khác nhau đọc được cùng event?
5. Vì sao 2 consumer cùng group không cùng nhận toàn bộ event?
6. Vì sao OrderId là key hợp lý cho order events?
7. Kafka có thay Outbox không?
8. Kafka có làm consumer không cần idempotency không?
```

Gợi ý đáp án:

```text
1. Topic là event stream/log, queue thiên về work cần xử lý rồi ack.
2. Vì mỗi partition là một ordered log riêng.
3. Offset là vị trí message trong partition, còn committed offset được track theo consumer group.
4. Mỗi group có offset riêng.
5. Cùng group là chia partition để scale xử lý.
6. Event cùng order vào cùng partition, giữ thứ tự theo order.
7. Không, DB và Kafka vẫn không chung transaction.
8. Không, consumer vẫn có thể replay/retry/rebalance.
```

---

## 23. Điều kiện pass bài

Bạn pass Buổi 25 khi:

```text
[ ] Kafka + Zookeeper chạy bằng docker compose.
[ ] Tạo được topic `microshop.order-events`.
[ ] Produce được JSON event vào topic.
[ ] Consume được event từ beginning.
[ ] Hiểu topic là named event stream.
[ ] Hiểu partition giúp scale và giữ ordering trong phạm vi partition.
[ ] Hiểu offset là vị trí message trong partition.
[ ] Hiểu consumer group có offset riêng.
[ ] Test được 2 group đọc cùng event độc lập.
[ ] Test được 2 consumer cùng group chia việc.
[ ] Xem được consumer group lag.
[ ] Hiểu vì sao OrderId nên làm key.
[ ] Ghi được `docs/kafka-intro-notes.md`.
```

Nếu hôm nay bạn chỉ nhớ một câu:

```text
Kafka không phải queue thường; Kafka là event log có partition, offset và consumer group.
```

là đã nắm được xương sống bài 25.

---

## 24. Điều kiện mở khóa Buổi 26

Bạn có thể sang Buổi 26 khi:

```text
[ ] Chạy Kafka local được.
[ ] Biết tạo topic.
[ ] Biết produce/consume event.
[ ] Hiểu topic/partition/offset/consumer group.
[ ] Hiểu Kafka sẽ phục vụ projection/read model.
```

Buổi 26 sẽ học:

```text
MongoDB Read Model
```

Mục tiêu Buổi 26:

```text
Thiết kế OrderSummaryReadModel.
Chạy MongoDB local.
Tạo Projection API đọc từ MongoDB hoặc chuẩn bị collection.
Hiểu MongoDB dùng cho read model/projection, không thay DB nghiệp vụ của OrderingService.
```
