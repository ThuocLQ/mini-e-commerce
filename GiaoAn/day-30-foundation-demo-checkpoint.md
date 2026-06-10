---
day: 30
title: "Foundation Demo + Checkpoint"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
encoding_note: "UTF-8 Markdown tiếng Việt chuẩn"
style: "learning-practice"
---

# Day 30: Foundation Demo + Checkpoint

## 0. Hôm nay học gì?

Hôm nay mình không học thêm pattern mới. Mình học cách chứng minh nền tảng MicroShop đang chạy được.

Day 30 giống một buổi kiểm tra giữa chặng: build được không, Docker chạy không, Kafka event đi được không, ProjectionWorker ghi MongoDB không, OrderQueryService đọc được không.

## 1. Vì sao cần bài này?

Nếu không có checkpoint, mình rất dễ học tiếp trên một nền bị lỗi mà không biết.

Trong thực tế, trước khi hardening hệ thống, team phải biết baseline hiện tại đang ổn tới đâu. Day 30 chính là baseline đó.

## 2. Khái niệm cốt lõi

### Foundation demo là gì?

Foundation demo là demo nhỏ nhưng end-to-end:

```text
Kafka event
-> ProjectionWorker
-> MongoDB read model
-> OrderQueryService
-> GET /order-summaries
```

### Checkpoint là gì?

Checkpoint là tài liệu ghi lại:

```text
Cái gì đã chạy được.
Cái gì chưa chạy được.
Cái gì chỉ mới training-stage.
Cái gì cần hardening ở Stage 2.
```

### Kafka lag là gì?

Lag là số message consumer chưa xử lý xong.

```text
Lag = 0 nghĩa là consumer đã bắt kịp.
Lag tăng mãi nghĩa là consumer đang có vấn đề hoặc xử lý không kịp.
```

## 3. Nhìn vào repo hiện tại

Các service chính:

```text
Services/ApiGateway
Services/CatalogService
Services/BasketService
Services/OrderingService
Services/DiscountService
Services/IdentityService
Services/PaymentService
Services/OrderQueryService
```

Các worker:

```text
Services/NotificationWorker
Workers/ProjectionWorker
```

Các project dùng chung:

```text
BuildingBlocks.Contracts
MicroShop.AppHost
MicroShop.ServiceDefaults
```

Hạ tầng local:

```text
PostgreSQL: lưu dữ liệu write-side
Redis: lưu basket/cache
RabbitMQ: workflow/task messaging
Kafka: event stream/projection learning
MongoDB: read model và projection failure
Docker Compose: chạy local runtime
Aspire AppHost: orchestration local .NET
```

Route/ID cũ không dùng lại:

```text
/orders/read-model
ORD-900
CUST-900
```


Flow chính của bài này:

```text
Kafka CLI demo producer
-> microshop.order-events
-> ProjectionWorker
-> MongoDB MicroShop_OrderReadDb.order_summaries
-> OrderQueryService
```

True lite mode không include `api-gateway` vì gateway có thể kéo thêm nhiều service khác.

## 4. Thực hành từng bước

### Bước 1: Build các project chính

```powershell
dotnet build Services/ApiGateway/ApiGateway.csproj
dotnet build Services/OrderQueryService/OrderQueryService.csproj
dotnet build Workers/ProjectionWorker/ProjectionWorker.csproj
```

Nếu hợp lý:

```powershell
dotnet build
```

### Bước 2: Start lite projection demo

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

### Bước 3: Check container/logs

```powershell
docker compose ps
docker compose logs projectionworker --tail 100
docker compose logs orderqueryservice --tail 100
```

### Bước 4: Tạo Kafka topic

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
```

### Bước 5: Produce OrderCreated event

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

Gửi:

```text
11111111-1111-1111-1111-111111111111:{"eventId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","eventType":"OrderCreated","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":1977.3,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:00:00Z"}
```

### Bước 6: Test read model

```text
GET {{order_query_url}}/order-summaries
GET {{order_query_url}}/order-summaries/11111111-1111-1111-1111-111111111111
```

### Bước 7: Check Kafka lag

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

### Bước 8: Viết checkpoint

Tạo:

```text
docs/checkpoints/stage-1-foundation-checkpoint.md
docs/backlog/stage-2-production-hardening-backlog.md
```

## 5. Kết quả kỳ vọng

Kỳ vọng:

```text
Build các project chính pass.
Docker containers chạy.
ProjectionWorker consume event.
MongoDB có order_summaries.
OrderQueryService trả được read model.
Kafka lag về 0.
Checkpoint report ghi rõ phần pass/fail.
```

## 6. Lỗi hay gặp

```text
Lỗi 1: Include api-gateway trong lite mode làm Docker kéo quá nhiều service.
Lỗi 2: Kafka topic chưa tạo nên producer/consumer không đúng.
Lỗi 3: Message key không phải orderId.
Lỗi 4: Payload thiếu customerName/itemCount/items.
Lỗi 5: Lag không về 0 nhưng không check ProjectionWorker logs.
```

## 7. Tổng kết bài học

Day 30 giúp mình biết foundation thật sự đang chạy. Sau bài này, mình có checkpoint để bước sang Stage 2 hardening mà không bị mơ hồ.

## 8. Checklist trước khi commit

```text
[ ] Hiểu được mục tiêu chính của bài.
[ ] Đã chạy các lệnh kiểm tra chính.
[ ] Đã tạo/cập nhật đúng docs hoặc code trong scope.
[ ] Đã test phần cần test.
[ ] Không dùng endpoint/id cũ.
[ ] Không claim production-ready.
[ ] Không làm lệch RabbitMQ/Kafka responsibility.
```

## 9. Commit/tag gợi ý

```text
Commit: Day 30: Foundation Demo Checkpoint
Tag: day-30-foundation-demo-checkpoint
```
