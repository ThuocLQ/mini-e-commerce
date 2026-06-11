---
day: 40
title: "Kafka Consumer Group + Rebalance"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
encoding_note: "UTF-8 Markdown tiếng Việt chuẩn"
style: "learning-practice-concise"
phase: "Stage 2.2 - Reliable Event-Driven Advanced"
---

# Day 40: Kafka Consumer Group + Rebalance

## 0. Mục tiêu bài học

Học cách Kafka chia việc cho nhiều consumer trong cùng một consumer group, vì sao có rebalance, và vì sao `ProjectionWorker` phải xử lý message theo hướng idempotent.

Sau bài này, bạn phải trả lời được:

```text
Consumer group là gì?
Partition ảnh hưởng gì tới scale?
Rebalance xảy ra khi nào?
Lag đọc ở đâu?
Vì sao key phải là orderId?
ProjectionWorker có rủi ro duplicate/replay ở đâu?
```

## 1. Sự thật repo hiện tại

```text
Topic: microshop.order-events
Consumer group: projection-worker
Consumer: Workers/ProjectionWorker
Read model DB: MongoDB
Read model collection: order_summaries
Failure collection: projection_failures
```

Event demo hợp lệ:

```text
OrderCreated
OrderPaid
OrderCancelled
```

Payload demo cần giữ đúng:

```text
eventId
eventType
orderId
customerId
customerName
totalAmount
currency
itemCount
items
occurredAtUtc
```

Không làm trong bài này:

```text
Không thêm Kafka retry topic.
Không thêm Kafka DLT.
Không thêm Schema Registry.
Không thêm OrderingService Kafka publisher.
Không claim exactly-once.
```

## 2. Khái niệm cần hiểu

### 2.1. Partition

Partition là “làn xử lý” của topic.

Kafka chỉ đảm bảo thứ tự message trong cùng một partition.

### 2.2. Consumer group

Consumer group là nhóm consumer cùng xử lý một topic.

Quy tắc:

```text
Một partition tại một thời điểm chỉ được assign cho một consumer trong cùng group.
```

Ví dụ:

```text
Topic 3 partition + 1 consumer  -> 1 consumer xử lý cả 3 partition.
Topic 3 partition + 3 consumer  -> mỗi consumer xử lý khoảng 1 partition.
Topic 3 partition + 5 consumer  -> chỉ 3 consumer có việc, 2 consumer idle.
```

Kết luận:

```text
Số partition giới hạn mức scale song song trong cùng consumer group.
```

### 2.3. Rebalance

Rebalance là lúc Kafka chia lại partition cho consumer.

Thường xảy ra khi:

```text
Consumer mới join group.
Consumer cũ chết.
Consumer timeout heartbeat.
Topic thay đổi số partition.
```

Rủi ro:

```text
Message đang xử lý có thể bị xử lý lại nếu offset chưa commit.
```

### 2.4. Vì sao key = orderId?

Kafka chọn partition dựa vào key.

Nếu key là `orderId`:

```text
Event của cùng một order thường vào cùng partition.
Thứ tự event theo order dễ giữ hơn.
```

Nếu không set key:

```text
Event cùng order có thể rơi vào partition khác nhau.
Projection có thể xử lý sai thứ tự.
```

## 3. Thực hành

### Bước 1: Inspect Kafka config của ProjectionWorker

```powershell
Get-ChildItem Workers/ProjectionWorker -Recurse -Filter *.cs |
  Select-String -Pattern "ConsumerConfig|GroupId|AutoOffsetReset|EnableAutoCommit|Subscribe|Commit|Consume|Partition|Offset|Kafka"
```

Ghi lại:

```text
GroupId = ?
EnableAutoCommit = true/false?
Commit offset nằm ở đâu?
Có log partition/offset không?
```

### Bước 2: Chạy lite runtime

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

### Bước 3: Tạo và kiểm tra topic

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --describe --topic microshop.order-events
```

Ghi lại:

```text
Partition count = ?
Replication factor = ?
```

### Bước 4: Kiểm tra consumer group

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Cần hiểu các cột:

```text
CURRENT-OFFSET: offset consumer đã xử lý/commit tới.
LOG-END-OFFSET: offset mới nhất của partition.
LAG: số message consumer còn chưa xử lý.
CONSUMER-ID: consumer đang giữ partition.
```

### Bước 5: Produce event có key = orderId

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

Gửi:

```text
11111111-1111-1111-1111-111111111111:{"eventId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","eventType":"OrderCreated","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer 1","totalAmount":100,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:00:00Z"}
```

### Bước 6: Kiểm tra worker và lag

```powershell
docker compose logs projectionworker --tail 100
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Kỳ vọng:

```text
ProjectionWorker consume event.
MongoDB order_summaries được cập nhật.
Lag về 0.
```

### Bước 7: Thử scale nếu Docker Compose cho phép

```powershell
docker compose up -d --scale projectionworker=2 projectionworker
```

Nếu lỗi do `container_name` hoặc compose không hỗ trợ scale, ghi vào docs. Không sửa lung tung trong bài này.

Nếu scale được, chạy lại:

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Quan sát partition assignment thay đổi.

## 4. Tài liệu cần tạo

```text
docs/messaging/kafka-consumer-group-rebalance-day-40.md
docs/messaging/kafka-projection-scale-notes.md
docs/backlog/day-40-kafka-consumer-hardening-backlog.md
```

Nội dung tối thiểu:

```text
Topic name.
Partition count.
Consumer group name.
EnableAutoCommit behavior.
Commit offset behavior.
Lag check command.
Scale test result.
Key = orderId rule.
Rebalance risk.
```

## 5. Lỗi hay gặp

```text
Lỗi 1: Nghĩ tăng consumer là luôn tăng throughput.
Lỗi 2: Quên partition count giới hạn scale.
Lỗi 3: Produce Kafka event không có key = orderId.
Lỗi 4: Lag không về 0 nhưng không xem ProjectionWorker logs.
Lỗi 5: Scale lỗi do Docker Compose rồi kết luận Kafka sai.
Lỗi 6: Claim exactly-once.
```

## 6. Kết luận cần nhớ

```text
Kafka scale theo partition.
Consumer group giúp chia partition cho nhiều consumer.
Rebalance có thể khiến message bị xử lý lại.
ProjectionWorker phải idempotent.
Key = orderId giúp giữ ordering theo từng order.
```

## 7. Checklist

```text
[ ] Inspect ProjectionWorker Kafka config.
[ ] Describe topic microshop.order-events.
[ ] Describe consumer group projection-worker.
[ ] Produce event với key = orderId.
[ ] Check ProjectionWorker logs.
[ ] Check lag về 0.
[ ] Document scale/rebalance behavior.
[ ] Không thêm Kafka DLT/retry topic.
[ ] Không thêm OrderingService Kafka publisher.
```

## 8. Commit/tag

```text
Commit: Day 40: Kafka Consumer Group Rebalance
Tag: day-40-kafka-consumer-group-rebalance
```
