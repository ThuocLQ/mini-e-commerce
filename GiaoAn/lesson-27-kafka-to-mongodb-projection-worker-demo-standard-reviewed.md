# Buoi 27: Kafka -> MongoDB ProjectionWorker cho MicroShop

## 0. Vi tri trong lo trinh

MicroShop hien tai da co:

```text
Buoi 23: OrderingService Outbox -> RabbitMQ -> NotificationWorker
Buoi 24: RabbitMQ vs Kafka decision
Buoi 25: Kafka local + topic / partition / offset / consumer group
Buoi 26: OrderQueryService + MongoDB read model
```

Bai 27 khong tao read API moi. Read API da nam trong `OrderQueryService`.

Bai nay them worker rieng de noi Kafka vao MongoDB read model:

```text
Kafka topic microshop.order-events
-> Workers/ProjectionWorker
-> MongoDB MicroShop_OrderReadDb.order_summaries
-> OrderQueryService GET /order-summaries
```

Cau nho:

```text
RabbitMQ xu ly workflow/task notification.
Kafka nuoi projection/read model.
MongoDB luu read model toi uu cho query.
OrderQueryService chi doc read model, khong consume Kafka.
```

---

## 1. Muc tieu bai hoc

Trong bai nay, muc tieu la:

```text
[ ] Tao Workers/ProjectionWorker.
[ ] Consume Kafka topic microshop.order-events.
[ ] Deserialize order projection event JSON.
[ ] Upsert MongoDB order_summaries.
[ ] Commit Kafka offset sau khi MongoDB apply thanh cong.
[ ] Ghi invalid/unsupported message vao projection_failures.
[ ] Verify read model bang OrderQueryService.
[ ] Hieu replay, idempotency co ban, va gioi han production cua projection.
```

Bai nay **chua lam**:

```text
[ ] Chua sua OrderingService Outbox de publish Kafka.
[ ] Chua dual-publish RabbitMQ + Kafka.
[ ] Chua thay RabbitMQ bang Kafka.
[ ] Chua lam schema registry.
[ ] Chua lam Kafka retry topic / DLT.
[ ] Chua lam rebuild read model command.
[ ] Chua lam processed EventId collection.
[ ] Chua them Elasticsearch.
```

Day la projection demo, khong pretend la production hoan chinh. Tuy vay, cach lam van giu cac rule production-minded:

```text
Disable Kafka auto commit.
MongoDB apply thanh cong roi moi commit offset.
Upsert theo OrderId de replay khong tao duplicate.
Invalid/unsupported message duoc ghi projection_failures roi commit de khong block partition trong bai hoc.
```

---

## 2. Target architecture

Truoc bai 27:

```text
Kafka da setup o bai 25.
MongoDB da setup o bai 26.
OrderQueryService da co read API /order-summaries.
RabbitMQ flow van dung cho NotificationWorker.
```

Sau bai 27:

```text
Kafka CLI produce order events
    |
    v
Workers/ProjectionWorker
    |
    v
MongoDB MicroShop_OrderReadDb.order_summaries
    |
    v
OrderQueryService
    |
    v
Postman / ApiGateway
```

Chi tiet:

```text
Kafka
topic: microshop.order-events
key: orderId
        |
        v
Workers/ProjectionWorker
consumer group: projection-worker
        |
        v
MongoDB
database: MicroShop_OrderReadDb
collection: order_summaries
        |
        v
OrderQueryService
GET /order-summaries
GET /order-summaries/{orderId}
```

Luu y quan trong:

```text
OrderingService van la write side/source of truth.
OrderQueryService la read side API.
ProjectionWorker la cau noi Kafka event stream -> MongoDB read model.
NotificationWorker tiep tuc consume RabbitMQ, khong lien quan bai nay.
```

---

## 3. Vi sao khong sua OrderingService o bai nay?

OrderingService hien da co Outbox Pattern de publish RabbitMQ cho NotificationWorker:

```text
OrderingService
-> OutboxMessages
-> RabbitMQ
-> NotificationWorker
```

Trong bai 27, ta chua sua flow do.

Ly do:

```text
RabbitMQ va Kafka dang phuc vu 2 muc dich khac nhau.
Bai 27 chi can chung minh Kafka -> MongoDB projection.
Kafka event duoc produce thu cong bang Kafka CLI.
Sau nay moi nang cap OrderingService OutboxPublisher de publish Kafka.
```

Khong nen lam tat ca trong mot bai:

```text
Neu vua them ProjectionWorker,
vua sua Outbox,
vua dual-publish Kafka/RabbitMQ,
thi kho debug va kho hieu offset/replay.
```

---

## 4. Read model hien tai

OrderQueryService dang doc MongoDB:

```text
Database: MicroShop_OrderReadDb
Collection: order_summaries
```

Endpoints hien tai:

```text
GET /order-summaries
GET /order-summaries/{orderId}
POST /debug/order-summaries    Development only
```

Bai 27 se khong dung endpoint verify cu trong giao an goc:

```text
GET /orders/read-model
```

Verify dung endpoint that hien tai:

```text
GET /order-summaries
GET /order-summaries/{orderId}
```

---

## 5. Event payload dung cho bai 27

Vi OrderingService chua publish Kafka that, bai nay dung projection demo envelope produce bang Kafka CLI.

Payload dung `Guid` cho `orderId` va `customerId` de khop model hien tai cua OrderQueryService. Cac id demo dang text custom trong giao an goc se duoc thay bang GUID.

Shape:

```json
{
  "eventId": "11111111-1111-1111-1111-111111111111",
  "eventType": "OrderCreated",
  "orderId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "customerId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  "customerName": "Demo Customer",
  "totalAmount": 1977.30,
  "currency": "VND",
  "itemCount": 1,
  "items": [
    {
      "productId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
      "productName": "MacBook Pro",
      "quantity": 1,
      "unitPrice": 1977.30
    }
  ],
  "occurredAtUtc": "2026-06-05T10:00:00Z"
}
```

Supported event types:

```text
OrderCreated
OrderPaid
OrderCancelled
```

Kafka key:

```text
key = orderId
```

Vi sao dung key = orderId?

```text
Kafka chi dam bao ordering trong cung partition.
Key = orderId giup event cua cung mot order di vao cung partition.
Projection theo tung order on dinh hon.
```

---

## 6. ProjectionWorker structure

Tao project:

```powershell
dotnet new worker -n ProjectionWorker -o Workers/ProjectionWorker
dotnet sln MicroShop.sln add Workers/ProjectionWorker/ProjectionWorker.csproj --solution-folder Workers
```

Structure mong muon:

```text
Workers/ProjectionWorker/
+-- Application/
|   +-- Abstractions/
|   +-- Events/
|   +-- Projections/
+-- Infrastructure/
|   +-- Kafka/
|   +-- MongoDb/
+-- Program.cs
+-- Worker.cs
+-- appsettings.Development.json
```

Packages:

```powershell
dotnet add Workers/ProjectionWorker/ProjectionWorker.csproj package Confluent.Kafka
dotnet add Workers/ProjectionWorker/ProjectionWorker.csproj package MongoDB.Driver
dotnet add Workers/ProjectionWorker/ProjectionWorker.csproj reference MicroShop.ServiceDefaults/MicroShop.ServiceDefaults.csproj
```

Giai thich layer:

```text
Application/Events
    DTO input tu Kafka.

Application/Projections
    Logic apply event thanh read model.

Application/Abstractions
    Interface repository / failure store.

Infrastructure/Kafka
    Kafka consumer options va worker loop.

Infrastructure/MongoDb
    MongoDB repository, indexes, projection_failures.
```

---

## 7. Kafka config

Local `appsettings.Development.json`:

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Topic": "microshop.order-events",
    "GroupId": "projection-worker",
    "AutoOffsetReset": "Earliest"
  }
}
```

Docker env:

```text
Kafka__BootstrapServers=kafka:29092
Kafka__Topic=microshop.order-events
Kafka__GroupId=projection-worker
Kafka__AutoOffsetReset=Earliest
```

Consumer config bat buoc:

```text
EnableAutoCommit = false
AutoOffsetReset = Earliest
```

Rule:

```text
MongoDB apply thanh cong -> commit Kafka offset.
MongoDB fail -> khong commit offset.
Invalid/unsupported payload -> ghi projection_failures -> commit offset.
```

Tai sao invalid/unsupported payload van commit?

```text
Trong bai hoc, topic co the co message test cu/sai schema.
Neu khong commit, worker se ket vinh vien tai message do va block partition.
Production that nen co retry topic / DLT / parking lot rieng.
```

---

## 8. MongoDB config

Local:

```json
{
  "MongoDb": {
    "ConnectionString": "mongodb://microshop:microshop@localhost:27017/?authSource=admin",
    "DatabaseName": "MicroShop_OrderReadDb",
    "OrderSummariesCollectionName": "order_summaries",
    "ProjectionFailuresCollectionName": "projection_failures"
  }
}
```

Docker:

```text
MongoDb__ConnectionString=mongodb://microshop:microshop@mongodb:27017/?authSource=admin
MongoDb__DatabaseName=MicroShop_OrderReadDb
MongoDb__OrderSummariesCollectionName=order_summaries
MongoDb__ProjectionFailuresCollectionName=projection_failures
```

Collections:

```text
order_summaries
projection_failures
```

Indexes:

```text
order_summaries:
    _id = orderId
    unique orderId
    createdAtUtc desc
    customerId + createdAtUtc desc

projection_failures:
    eventId
    occurredAtUtc desc
    topic + partition + offset unique
```

---

## 9. Projection behavior

### OrderCreated

Expected effect:

```text
Create or update one order summary document.
Status = Created only if document is new.
CreatedAtUtc is preserved if document already exists.
LastUpdatedAtUtc = occurredAtUtc if event is not older than current projection.
```

Important:

```text
Replay OrderCreated must not create duplicate document.
Replay OrderCreated must not downgrade Paid/Cancelled back to Created.
```

### OrderPaid

Expected effect:

```text
Status = Paid
PaidAtUtc = occurredAtUtc
LastUpdatedAtUtc = occurredAtUtc
```

If document does not exist:

```text
In bai 27, allow upsert minimal shell document by orderId.
This avoids losing paid event if OrderCreated was not consumed yet.
Production later should use aggregate sequence/version for stronger ordering.
```

### OrderCancelled

Expected effect:

```text
Status = Cancelled
CancelledAtUtc = occurredAtUtc
LastUpdatedAtUtc = occurredAtUtc
```

If document does not exist:

```text
Allow upsert minimal shell document by orderId.
```

### Out-of-order basic guard

If incoming event has:

```text
occurredAtUtc < lastProjectedEventOccurredAtUtc
```

then:

```text
Do not overwrite newer status.
Log warning.
Commit offset after recording/ignoring safely.
```

This is not full production ordering, but prevents the worst downgrade case in demo.

---

## 10. Projection metadata

Order summary document should include optional projection metadata:

```text
lastProjectedEventId
lastProjectedEventType
lastProjectedEventOccurredAtUtc
lastProjectedAtUtc
paidAtUtc
cancelledAtUtc
```

Why:

```text
Help debug replay and event application.
Help avoid older event overwriting newer state.
Prepare for processed-event collection later.
```

OrderQueryService response can expose these fields as optional fields if the read contract is updated.

---

## 11. Projection failure handling

Create collection:

```text
projection_failures
```

Store a failure record when:

```text
JSON cannot deserialize.
Required fields missing.
eventType unsupported.
Guid fields invalid.
```

Failure record should include:

```text
id
eventId if available
topic
partition
offset
key
rawValue
error
occurredAtUtc
createdAtUtc
```

Rule:

```text
Invalid/unsupported message -> save projection failure -> commit offset.
MongoDB unavailable -> do not save failure as poison; do not commit offset.
```

Production note:

```text
In production, invalid/unsupported messages should normally go to Kafka DLT/retry topic or parking lot.
projection_failures is acceptable for this training stage.
```

---

## 12. Docker compose

Add service:

```yaml
projectionworker:
  build:
    context: .
    dockerfile: Dockerfile.service
    args:
      PROJECT_PATH: Workers/ProjectionWorker/ProjectionWorker.csproj
      PROJECT_DLL: ProjectionWorker.dll
  container_name: microshop-projectionworker
  environment:
    DOTNET_ENVIRONMENT: Development
    Kafka__BootstrapServers: kafka:29092
    Kafka__Topic: microshop.order-events
    Kafka__GroupId: projection-worker
    Kafka__AutoOffsetReset: Earliest
    MongoDb__ConnectionString: mongodb://microshop:microshop@mongodb:27017/?authSource=admin
    MongoDb__DatabaseName: MicroShop_OrderReadDb
    MongoDb__OrderSummariesCollectionName: order_summaries
    MongoDb__ProjectionFailuresCollectionName: projection_failures
  depends_on:
    kafka:
      condition: service_healthy
    mongodb:
      condition: service_healthy
  networks:
    - microshop-network
```

Luu y:

```text
ProjectionWorker khong expose HTTP API bat buoc.
Neu them health endpoint sau nay thi bai 28 xu ly.
```

---

## 13. AppHost

Add `ProjectionWorker` vao Aspire:

```text
builder.AddProject<Projects.ProjectionWorker>("ProjectionWorker")
    .WithEnvironment("Kafka__BootstrapServers", "localhost:9092")
    .WithEnvironment("Kafka__Topic", "microshop.order-events")
    .WithEnvironment("Kafka__GroupId", "projection-worker")
    .WithEnvironment("Kafka__AutoOffsetReset", "Earliest")
    .WithEnvironment("MongoDb__ConnectionString", "mongodb://microshop:microshop@localhost:27017/?authSource=admin")
    .WithEnvironment("MongoDb__DatabaseName", "MicroShop_OrderReadDb")
    .WithEnvironment("MongoDb__OrderSummariesCollectionName", "order_summaries")
    .WithEnvironment("MongoDb__ProjectionFailuresCollectionName", "projection_failures")
    .WaitFor(mongodb);
```

Neu AppHost chua co Kafka container resource, bai 27 co the chay Kafka bang docker compose rieng.

Acceptable local flow:

```text
docker compose up -d zookeeper kafka mongodb
dotnet run --project Workers/ProjectionWorker/ProjectionWorker.csproj
dotnet run --project Services/OrderQueryService/OrderQueryService.csproj
```

---

## 14. Build test

Build worker:

```powershell
dotnet build Workers\ProjectionWorker\ProjectionWorker.csproj --no-restore --nologo -v minimal
```

Build solution:

```powershell
dotnet build MicroShop.sln --no-restore --nologo -v minimal
```

---

## 15. Docker test setup

Start infra and services:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

Create topic:

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
```

List topics:

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --list
```

Describe topic:

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --describe --topic microshop.order-events
```

Open keyed producer:

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

---

## 16. Test OrderCreated

Use:

```text
orderId = aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
customerId = bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb
productId = cccccccc-cccc-cccc-cccc-cccccccccccc
```

Produce:

```text
aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:{"eventId":"11111111-1111-1111-1111-111111111111","eventType":"OrderCreated","orderId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","customerId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","customerName":"Demo Customer","totalAmount":1977.30,"currency":"VND","itemCount":1,"items":[{"productId":"cccccccc-cccc-cccc-cccc-cccccccccccc","productName":"MacBook Pro","quantity":1,"unitPrice":1977.30}],"occurredAtUtc":"2026-06-05T10:00:00Z"}
```

Verify direct service in Postman:

```text
GET {{orderQueryBaseUrl}}/order-summaries/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
```

Verify gateway in Postman:

```text
GET {{gatewayBaseUrl}}/order-summaries/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
```

Expected:

```text
status = Created
totalAmount = 1977.30
currency = VND
items[0].productName = MacBook Pro
lastProjectedEventId = 11111111-1111-1111-1111-111111111111
```

---

## 17. Test OrderPaid

Produce same key:

```text
aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:{"eventId":"22222222-2222-2222-2222-222222222222","eventType":"OrderPaid","orderId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","customerId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","customerName":"Demo Customer","totalAmount":1977.30,"currency":"VND","itemCount":1,"items":[{"productId":"cccccccc-cccc-cccc-cccc-cccccccccccc","productName":"MacBook Pro","quantity":1,"unitPrice":1977.30}],"occurredAtUtc":"2026-06-05T10:05:00Z"}
```

Verify:

```text
GET {{orderQueryBaseUrl}}/order-summaries/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
```

Expected:

```text
status = Paid
paidAtUtc = 2026-06-05T10:05:00Z
lastProjectedEventType = OrderPaid
```

---

## 18. Test OrderCancelled

Use another order:

```text
orderId = dddddddd-dddd-dddd-dddd-dddddddddddd
customerId = eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee
```

Produce Created:

```text
dddddddd-dddd-dddd-dddd-dddddddddddd:{"eventId":"33333333-3333-3333-3333-333333333333","eventType":"OrderCreated","orderId":"dddddddd-dddd-dddd-dddd-dddddddddddd","customerId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","customerName":"Cancel Customer","totalAmount":500.00,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-06-05T10:10:00Z"}
```

Produce Cancelled:

```text
dddddddd-dddd-dddd-dddd-dddddddddddd:{"eventId":"44444444-4444-4444-4444-444444444444","eventType":"OrderCancelled","orderId":"dddddddd-dddd-dddd-dddd-dddddddddddd","customerId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","customerName":"Cancel Customer","totalAmount":500.00,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-06-05T10:15:00Z"}
```

Verify:

```text
GET {{orderQueryBaseUrl}}/order-summaries/dddddddd-dddd-dddd-dddd-dddddddddddd
```

Expected:

```text
status = Cancelled
cancelledAtUtc = 2026-06-05T10:15:00Z
```

---

## 19. Test invalid unsupported event

Produce:

```text
aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:{"eventId":"55555555-5555-5555-5555-555555555555","eventType":"OrderRefunded","orderId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","customerId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","occurredAtUtc":"2026-06-05T10:20:00Z"}
```

Expected:

```text
ProjectionWorker does not crash.
Offset commits after failure is recorded.
MongoDB projection_failures has one document for unsupported event.
Order summary status is not changed.
```

Check Mongo shell:

```powershell
docker exec -it microshop-mongodb mongosh -u microshop -p microshop --authenticationDatabase admin
```

```javascript
use MicroShop_OrderReadDb
db.projection_failures.find().sort({ createdAtUtc: -1 }).limit(5).pretty()
```

---

## 20. Offset and lag verification

Run:

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Expected:

```text
CURRENT-OFFSET tang sau khi worker process thanh cong.
LAG ve 0 khi khong con message cho.
```

Rule:

```text
Commit offset sau khi MongoDB apply/failure-record thanh cong.
Khong commit offset neu MongoDB unavailable.
```

---

## 21. Replay verification

Change group id:

```text
Kafka__GroupId=projection-worker-replay-demo
```

Restart worker.

Expected:

```text
Group moi chua co offset nen doc lai event tu beginning neu Kafka retention con.
MongoDB khong tao duplicate vi _id/orderId khong doi.
OrderCreated replay khong downgrade Paid/Cancelled ve Created.
```

Verify:

```text
GET {{orderQueryBaseUrl}}/order-summaries/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
```

Expected:

```text
One document only.
Status remains Paid.
```

---

## 22. Review production fit

Bai 27 da dat muc production-minded co ban:

```text
ProjectionWorker tach rieng, khong nhot logic vao OrderQueryService.
Kafka auto commit disabled.
Offset commit after Mongo side effect.
Mongo upsert by OrderId.
Projection metadata de debug.
Invalid messages vao projection_failures.
RabbitMQ workflow khong bi thay doi.
Kafka dung cho projection/read model.
```

Nhung chua phai production full:

```text
Chua co Kafka retry topic / DLT.
Chua co schema registry.
Chua co rebuild command.
Chua co processed EventId collection.
Chua co aggregate version/sequence de xu ly out-of-order chuan.
Chua co lag dashboard/alert.
Chua co OrderingService Kafka publisher.
Chua co Elasticsearch.
```

Stage sau nen nang cap:

```text
OrderingService Outbox -> publish Kafka.
ProjectionWorker DLT/retry topics.
Projection rebuild from beginning.
ProcessedEvent collection.
Schema versioning.
Lag metrics + alert.
```

---

## 23. Dieu kien pass bai

Ban pass bai 27 khi:

```text
[ ] Workers/ProjectionWorker build duoc.
[ ] ProjectionWorker connect Kafka duoc.
[ ] ProjectionWorker connect MongoDB duoc.
[ ] Consume topic microshop.order-events.
[ ] Produce OrderCreated bang Kafka CLI.
[ ] MongoDB co document trong order_summaries.
[ ] OrderQueryService GET /order-summaries/{orderId} doc duoc document.
[ ] Gateway GET /order-summaries/{orderId} doc duoc document.
[ ] Produce OrderPaid, status thanh Paid.
[ ] Produce OrderCancelled, status thanh Cancelled.
[ ] Unsupported event duoc ghi projection_failures va worker tiep tuc chay.
[ ] Consumer group lag ve 0 sau khi process.
[ ] Replay bang group moi khong tao duplicate document.
[ ] Giai thich duoc vi sao commit offset sau MongoDB apply.
```

Neu chi nho mot cau:

```text
ProjectionWorker bien Kafka event stream thanh MongoDB read model, va chi commit offset sau khi projection side effect thanh cong.
```

---

## 24. Dieu kien mo khoa bai 28

Co the sang bai 28 khi:

```text
[ ] Hieu Kafka -> MongoDB projection flow.
[ ] Hieu OrderQueryService la read API, ProjectionWorker la updater.
[ ] Hieu replay can idempotency.
[ ] Hieu invalid message va MongoDB failure phai xu ly khac nhau.
[ ] Hieu RabbitMQ va Kafka dang phuc vu hai muc dich rieng.
```

Bai 28 se tap trung hon vao:

```text
Logging.
Health checks.
Operational visibility.
Runbook/debug checklist.
```
