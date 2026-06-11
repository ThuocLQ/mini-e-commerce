---
day: 41
title: "MongoDB Projection Rebuild"
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

# Day 41: MongoDB Projection Rebuild

## 0. Mục tiêu bài học

Học cách rebuild MongoDB read model khi projection bị sai, schema thay đổi, hoặc cần tạo lại dữ liệu query.

Sau bài này, bạn phải trả lời được:

```text
Read model là gì?
Projection rebuild là gì?
Khi nào cần rebuild?
Vì sao không nên drop collection thật bừa bãi?
Replay Kafka bị giới hạn bởi gì?
Projection cần idempotent ở đâu?
```

## 1. Sự thật repo hiện tại

```text
Worker: Workers/ProjectionWorker
Read API: Services/OrderQueryService
Kafka topic: microshop.order-events
MongoDB database: MicroShop_OrderReadDb
Read collection: order_summaries
Failure collection: projection_failures
```

Event types hiện có:

```text
OrderCreated
OrderPaid
OrderCancelled
```

Payload demo hợp lệ:

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

Chưa được tự claim nếu chưa inspect code:

```text
Có processed_projection_events collection.
Có rebuild command production-ready.
Kafka là event store vĩnh viễn.
Projection exactly-once.
```

## 2. Khái niệm cần hiểu

### 2.1. Read model

Read model là dữ liệu phục vụ query nhanh.

Trong bài này:

```text
order_summaries là read model.
OrderQueryService đọc order_summaries.
ProjectionWorker ghi order_summaries.
```

### 2.2. Projection

Projection là quá trình biến event thành read model.

Ví dụ:

```text
OrderCreated -> tạo summary.
OrderPaid -> cập nhật trạng thái paid.
OrderCancelled -> cập nhật trạng thái cancelled.
```

### 2.3. Rebuild

Rebuild là tạo lại read model.

Các trường hợp cần rebuild:

```text
Projection logic cũ có bug.
Read model thiếu field mới.
MongoDB schema thay đổi.
Data trong order_summaries bị sai.
Cần tạo collection mới phục vụ query mới.
```

### 2.4. Replay risk

Replay event không đơn giản vì có các rủi ro:

```text
Duplicate event.
Event đến sai thứ tự.
Event schema cũ.
Kafka retention không còn đủ event.
MongoDB write giữa chừng fail.
```

Kết luận:

```text
Rebuild cần runbook, không làm bừa bằng cách drop collection thật.
```

## 3. Thực hành

### Bước 1: Inspect ProjectionWorker

```powershell
Get-ChildItem Workers/ProjectionWorker -Recurse -Filter *.cs |
  Select-String -Pattern "OrderCreated|OrderPaid|OrderCancelled|order_summaries|projection_failures|ReplaceOne|UpdateOne|Upsert|eventId|occurredAtUtc|Commit"
```

Ghi lại:

```text
Projection ghi MongoDB bằng ReplaceOne, UpdateOne hay insert?
Có upsert theo orderId không?
Có check duplicate eventId không?
Có lưu lastEventAtUtc không?
Có commit offset sau MongoDB write không?
```

### Bước 2: Inspect OrderQueryService

```powershell
Get-ChildItem Services/OrderQueryService -Recurse -Filter *.cs |
  Select-String -Pattern "OrderSummary|Mongo|IMongoCollection|order_summaries|projection_failures"
```

Ghi lại:

```text
Collection name thật.
Model field thật.
Endpoint đọc read model.
```

### Bước 3: Inspect contract/envelope

```powershell
Get-ChildItem BuildingBlocks.Contracts -Recurse -Filter *.cs |
  Select-String -Pattern "OrderCreated|OrderPaid|OrderCancelled|MicroShopEventEnvelope|eventId|eventVersion"
```

### Bước 4: Chạy lite runtime

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

Check read model:

```text
GET {{order_query_url}}/order-summaries
```

Không drop collection thật trong bài này.

### Bước 5: Viết rebuild strategy

Tạo:

```text
docs/projections/mongodb-projection-rebuild-day-41.md
```

Ghi 3 option:

```text
Option A: Replay Kafka từ earliest bằng consumer group mới.
Option B: Rebuild vào collection mới rồi swap.
Option C: Rebuild từ write-side export/tool có kiểm soát.
```

Đánh giá nhanh:

```text
Option A dễ demo nhưng phụ thuộc Kafka retention.
Option B an toàn hơn vì không phá collection hiện tại.
Option C có thể vi phạm service boundary nếu làm bừa.
```

### Bước 6: Viết safe rebuild runbook

Runbook training-stage:

```text
1. Không drop order_summaries thật nếu chưa backup/confirm.
2. Tạo collection mới: order_summaries_rebuild.
3. Dùng consumer group mới để replay từ earliest.
4. Apply event vào collection mới.
5. Ghi invalid event vào projection_failures_rebuild hoặc document handling.
6. Verify sample records.
7. Compare count/status.
8. Chỉ swap khi đã verify.
```

### Bước 7: Viết replay safety review

Tạo:

```text
docs/projections/projection-replay-safety-review.md
```

Checklist review:

```text
Duplicate eventId xử lý thế nào?
OrderPaid trước OrderCreated thì sao?
Older event có overwrite newer state không?
Có cần processed_projection_events không?
Có cần aggregateVersion/sequence không?
Kafka retention có đủ để rebuild không?
```

### Bước 8: Tạo backlog

Tạo:

```text
docs/backlog/day-41-projection-rebuild-backlog.md
```

Backlog tối thiểu:

```text
[ ] Add rebuild command/tool.
[ ] Support rebuild to separate collection.
[ ] Add processed_projection_events collection.
[ ] Add unique index on eventId.
[ ] Add aggregateVersion/sequence later.
[ ] Add eventVersion/schemaVersion handling.
[ ] Add rebuild verification report.
[ ] Document Kafka retention limitation.
```

## 4. Lỗi hay gặp

```text
Lỗi 1: Drop order_summaries thật khi chưa backup.
Lỗi 2: Nghĩ Kafka luôn lưu event vĩnh viễn.
Lỗi 3: Rebuild thẳng vào collection đang phục vụ query.
Lỗi 4: Không nghĩ tới duplicate event.
Lỗi 5: Không nghĩ tới out-of-order event.
Lỗi 6: Không kiểm tra projection_failures.
Lỗi 7: Claim projection exactly-once.
```

## 5. Kết luận cần nhớ

```text
Read model là dữ liệu phái sinh.
Dữ liệu phái sinh có thể rebuild.
Rebuild an toàn nên ưu tiên collection mới rồi verify/swap.
Kafka replay bị giới hạn bởi retention.
Projection phải chịu được duplicate và replay.
```

## 6. Checklist

```text
[ ] Inspect ProjectionWorker.
[ ] Inspect OrderQueryService Mongo model.
[ ] Inspect event contract/envelope.
[ ] Document rebuild options.
[ ] Document safe rebuild runbook.
[ ] Document replay safety review.
[ ] Create day-41 projection rebuild backlog.
[ ] Không drop collection thật.
[ ] Không claim Kafka là event store vĩnh viễn.
[ ] Không claim projection exactly-once.
```

## 7. Commit/tag

```text
Commit: Day 41: MongoDB Projection Rebuild
Tag: day-41-mongodb-projection-rebuild
```
