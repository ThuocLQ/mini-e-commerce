---
day: 40
title: "Kafka Projection Reliability + Rebalance Risk"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
style: "production-failure-scenario"
phase: "Stage 2 - Production Hardening"
---

# Day 40: Kafka Projection Reliability + Rebalance Risk

## 0. Vấn đề production

Không học lại Kafka basic.

Vấn đề thật:

```text
ProjectionWorker consume event từ Kafka để ghi MongoDB read model.
Worker có thể crash/restart/rebalance.
Message có thể bị xử lý lại.
Nếu projection không idempotent, order_summaries có thể sai.
```

## 1. Kiến thức cần nắm

### 1.1. Kafka consumer thường phải chấp nhận at-least-once

```text
At-least-once:
    Message có thể xử lý hơn 1 lần.

At-most-once:
    Message có thể mất nếu commit offset quá sớm.

Exactly-once:
    Không được claim nếu chưa chứng minh end-to-end.
```

Hướng an toàn cho MicroShop:

```text
Chấp nhận at-least-once.
Làm projection idempotent.
```

### 1.2. Offset commit là điểm rủi ro

Có 2 việc:

```text
1. Ghi MongoDB.
2. Commit Kafka offset.
```

Case nguy hiểm:

```text
Commit offset trước.
Ghi MongoDB fail.
=> Kafka không gửi lại.
=> Read model thiếu data.
```

Case dễ kiểm soát hơn:

```text
Ghi MongoDB xong.
Worker crash trước khi commit offset.
=> Kafka gửi lại.
=> Projection phải chịu duplicate.
```

### 1.3. Rebalance risk

Rebalance có thể xảy ra khi:

```text
Consumer join/leave group.
Heartbeat timeout.
Container restart.
Scale worker.
```

Hệ quả:

```text
Partition bị revoke/assign lại.
Message chưa commit có thể được consume lại.
```

### 1.4. key = orderId

Không phải để chống duplicate.

Nó giúp:

```text
Event cùng order có xu hướng vào cùng partition.
Thứ tự event theo từng order dễ giữ hơn.
```

Không giúp:

```text
Không chống duplicate.
Không chống event cũ overwrite event mới.
Không thay thế idempotency.
```

## 2. Repo cần nhìn vào đâu

```powershell
Get-ChildItem Workers/ProjectionWorker -Recurse -Filter *.cs |
  Select-String -Pattern "ConsumerConfig|GroupId|EnableAutoCommit|Subscribe|Consume|Commit|OrderCreated|OrderPaid|OrderCancelled|Mongo|Upsert|ReplaceOne|UpdateOne"
```

Cần xác nhận:

```text
GroupId có phải projection-worker không?
EnableAutoCommit true hay false?
Commit offset đang làm ở đâu?
Projection ghi MongoDB bằng Upsert/ReplaceOne/UpdateOne hay Insert?
Có check eventId duplicate chưa?
Có log partition/offset không?
Có log assigned/revoked/lost partitions khi consumer group rebalance không?
Có log commit offset thành công sau khi apply MongoDB không?
```

## 3. Thực hành

### Bước 1: Chạy lite runtime

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

### Bước 2: Tạo/check topic

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --describe --topic microshop.order-events
```

### Bước 3: Check consumer group lag

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Cần hiểu:

```text
CURRENT-OFFSET
LOG-END-OFFSET
LAG
```

### Bước 4: Produce event có key = orderId

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

Gửi:

```text
11111111-1111-1111-1111-111111111111:{"eventId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","eventType":"OrderCreated","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":100,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:00:00Z"}
```

### Bước 5: Check worker và API

```powershell
docker compose logs projectionworker --tail 100
```

```text
GET {{order_query_url}}/order-summaries
GET {{order_query_url}}/order-summaries/11111111-1111-1111-1111-111111111111
```

### Bước 6: Produce duplicate event

Gửi lại đúng message trên.

Cần quan sát:

```text
Có duplicate document không?
Status/amount có bị sai không?
Worker log duplicate hay xử lý lại im lặng?
```

## 4. Failure drill

Drill chính:

```text
Produce cùng một eventId 2 lần.
```

Phân loại kết quả:

```text
Safe một phần:
    Upsert theo orderId, không nhân đôi document.

Chưa đủ safe:
    Không lưu processed eventId.
    Không ngăn event cũ overwrite event mới.

Risk:
    Insert tạo duplicate document.
    Duplicate làm sai status/amount.
```

Không kết luận “projection safe” nếu chỉ thấy không duplicate document. Cần xem event ordering và processed-event strategy.

## 5. Câu hỏi interview

```text
1. Kafka consumer nên commit offset trước hay sau DB write?
2. Nếu worker crash sau DB write nhưng trước commit offset thì sao?
3. Rebalance có thể gây duplicate như thế nào?
4. key=orderId giúp gì và không giúp gì?
5. Lag tăng thì debug từ đâu?
6. Vì sao không claim exactly-once?
7. Projection idempotent nghĩa là gì?
```

## 6. Kết luận

```text
Kafka projection reliability = xử lý được crash/rebalance/duplicate/lag.
Key = orderId chỉ hỗ trợ ordering theo order.
Idempotency phải nằm ở projection logic.
```

## 7. Optional commit

```text
Commit: Day 40: Kafka Projection Reliability Rebalance Risk
Tag: day-40-kafka-projection-reliability
```
