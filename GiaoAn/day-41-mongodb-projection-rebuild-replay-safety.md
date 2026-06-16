---
day: 41
title: "MongoDB Projection Rebuild + Replay Safety"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
style: "production-failure-scenario"
phase: "Stage 2 - Production Hardening"
---

# Day 41: MongoDB Projection Rebuild + Replay Safety

## 0. Vấn đề production

`order_summaries` là read model phái sinh. Nó có thể sai nếu projection logic có bug, event từng xử lý lỗi, schema read model thay đổi, hoặc consumer từng bị down.

Vấn đề của bài này:

```text
Rebuild read model thế nào mà không phá dữ liệu đang phục vụ query?
Replay Kafka có đủ event không?
Duplicate/out-of-order event xử lý thế nào?
```

## 1. Kiến thức cần nắm

### 1.1. Kafka retention là giới hạn lớn

Kafka không tự động là event store vĩnh viễn.

```text
Replay từ earliest chỉ replay được event còn trong Kafka.
Nếu retention không đủ, rebuild có thể thiếu lịch sử.
```

Kết luận:

```text
Muốn rebuild đầy đủ phải biết retention policy hoặc có nguồn event/source of truth khác.
```

### 1.2. Rebuild an toàn nên dùng collection mới

Không rebuild thẳng vào `order_summaries` đang phục vụ query.

```text
Build vào order_summaries_rebuild.
Verify.
Cutover/swap khi pass.
```

### 1.3. Replay safety

Replay có thể gặp:

```text
Duplicate event.
Out-of-order event.
Old event version.
Partial MongoDB write failure.
```

Cần nghĩ tới:

```text
eventId / processed_projection_events
occurredAtUtc / aggregateVersion / sequence
projection_failures
schemaVersion/eventVersion
```

## 2. Repo cần nhìn vào đâu

```powershell
Get-ChildItem Workers/ProjectionWorker -Recurse -Filter *.cs |
  Select-String -Pattern "eventId|occurredAtUtc|OrderCreated|OrderPaid|OrderCancelled|order_summaries|projection_failures|ReplaceOne|UpdateOne|Upsert|Commit"

Get-ChildItem Services/OrderQueryService -Recurse -Filter *.cs |
  Select-String -Pattern "OrderSummary|Mongo|IMongoCollection|order_summaries|projection_failures"
```

Cần xác nhận:

```text
OrderSummary có field nào?
Có lastEventId/lastEventAtUtc không?
Có processed_projection_events không?
projection_failures đang lưu gì?
ProjectionWorker có rebuild mode không?
Rebuild worker dùng consumer group riêng không?
```

## 3. Lab cụ thể

### Bước 1: Chạy lite runtime

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

### Bước 2: Check read model hiện tại

```text
GET {{order_query_url}}/order-summaries
```

### Bước 3: Chạy rebuild worker profile

Repo hiện có `projectionworker-rebuild` trong Docker Compose profile `projection-rebuild`.

Service này:

```text
Kafka__GroupId = projection-worker-rebuild-demo
MongoDb__RebuildModeEnabled = true
MongoDb__RebuildOrderSummariesCollectionName = order_summaries_rebuild
```

Chạy:

```powershell
docker compose --profile projection-rebuild up -d --build projectionworker-rebuild
```

Kiểm tra log:

```powershell
docker compose logs projectionworker-rebuild --tail 100
```

Kỳ vọng log có:

```text
RebuildMode=True
TargetCollection=order_summaries_rebuild
GroupId=projection-worker-rebuild-demo
```

### Bước 4: Verify rebuild collection trong MongoDB

Tìm tên container MongoDB:

```powershell
docker ps --format "table {{.Names}}	{{.Image}}	{{.Ports}}"
```

Nếu container tên `microshop-mongodb`:

```powershell
docker exec -it microshop-mongodb mongosh
```

Trong `mongosh`:

```javascript
use MicroShop_OrderReadDb

db.order_summaries_rebuild.drop()
db.createCollection("order_summaries_rebuild")
db.order_summaries_rebuild.createIndex({ orderId: 1 }, { unique: true })
db.order_summaries_rebuild.getIndexes()
```

Nếu rebuild worker đã start trước đó, initializer cũng có thể tự tạo index. Lệnh trên dùng để kiểm tra/thử lại trong lab.

### Bước 5: Produce/replay event demo

Mở Kafka producer:

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

Gửi event:

```text
11111111-1111-1111-1111-111111111111:{"eventId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","eventType":"OrderCreated","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":100,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:00:00Z"}
```

Logic cần hiểu:

```text
projectionworker ghi vào order_summaries.
projectionworker-rebuild ghi vào order_summaries_rebuild.
Hai worker dùng group khác nhau nên rebuild group có thể replay từ earliest mà không kéo offset của live projection group.
```

### Bước 6: Verify live và rebuild collection

Trong `mongosh`:

```javascript
use MicroShop_OrderReadDb

db.order_summaries.find({ orderId: "11111111-1111-1111-1111-111111111111" }).pretty()
db.order_summaries_rebuild.find({ orderId: "11111111-1111-1111-1111-111111111111" }).pretty()
db.projection_failures.find().sort({ _id: -1 }).limit(5).pretty()
```

### Bước 7: Test duplicate replay

Produce lại cùng event.

Verify:

```javascript
db.order_summaries.find({ orderId: "11111111-1111-1111-1111-111111111111" }).count()
db.order_summaries_rebuild.find({ orderId: "11111111-1111-1111-1111-111111111111" }).count()
```

Kỳ vọng tối thiểu:

```text
Không có nhiều document cùng orderId.
Upsert theo orderId chống duplicate document.
Logic lastProjectedEventOccurredAtUtc giúp tránh old event overwrite newer state theo occurredAtUtc.
Vẫn chưa có processed_projection_events để track từng eventId đã xử lý.
```

### Bước 8: Ghi kết luận repo thật

Ghi ngay trong bài/note:

```text
Current repo:
- Có rebuild mode bằng config + compose profile projection-rebuild.
- Có rebuild collection order_summaries_rebuild.
- Chưa có processed_projection_events.
- Có lastProjectedEventOccurredAtUtc để chặn old event overwrite theo timestamp.
- Chưa có aggregateVersion/sequence.
- Kafka retention hiện chưa xác minh / đã xác minh.
- Rebuild thật cần thêm gì.
```

## 4. Failure drill

```text
Mô phỏng replay bằng duplicate event + verify MongoDB collection.
```

Phân loại:

```text
Safe:
    Không duplicate document.
    Không overwrite newer state bằng older event.
    Invalid event vào projection_failures.

Risk:
    Event cũ overwrite event mới.
    Duplicate làm sai trạng thái.
    Không có cách biết event đã xử lý.
```

## 5. Câu hỏi interview

```text
1. Vì sao read model có thể rebuild?
2. Vì sao Kafka retention là rủi ro?
3. Vì sao rebuild vào collection mới an toàn hơn?
4. Rebuild mode hiện tại đã giải quyết được gì và còn thiếu gì?
5. Duplicate eventId xử lý thế nào?
6. Khi nào cần processed_projection_events?
7. Khi nào cần aggregateVersion/sequence?
8. Nếu Kafka thiếu event cũ thì rebuild bằng gì?
```

## 6. Kết luận

```text
Projection rebuild không chỉ là viết strategy.
Lab tối thiểu phải tạo rebuild collection, produce/replay event, verify data và ghi rõ gap.
Repo hiện đã có rebuild mode cơ bản bằng config + compose profile, nhưng chưa phải rebuild production-safe đầy đủ vì còn thiếu processed_projection_events, aggregateVersion/sequence và cutover/swap automation.
```

## 7. Optional commit

```text
Commit: Day 41: MongoDB Projection Rebuild Replay Safety
Tag: day-41-mongodb-projection-rebuild
```
