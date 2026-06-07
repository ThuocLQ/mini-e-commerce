# MicroShop Foundation Demo Script

This script is the Day 30 foundation demo.

It proves the current Kafka projection read-model flow without pretending the project is production ready.

## 1. Start Lite Projection Demo

True lite mode starts only the projection path:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

This mode proves:

```text
Kafka topic
ProjectionWorker
MongoDB read model
OrderQueryService direct API
Kafka lag
```

`ApiGateway` is not included in true lite mode because it depends on many services in `docker-compose.yml`.

## 2. Optional Gateway Mode

Use this only if you want to test gateway routes without starting the whole system:

```powershell
docker compose up -d --build --no-deps api-gateway
```

If gateway has missing downstream services, switch to full system mode.

## 3. Start Full System

Use this when demoing all services:

```powershell
docker compose up -d --build
```

## 4. Verify Health

Lite mode:

```text
GET {{order_query_url}}/health
```

Full system or optional gateway mode:

```text
GET {{gateway_url}}/health
```

`ApiGateway` maps `/health` explicitly for Docker.

## 5. Create Kafka Topic

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
```

Describe the topic:

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --describe --topic microshop.order-events
```

## 6. Produce Demo Order Events

Start producer:

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

OrderCreated:

```text
11111111-1111-1111-1111-111111111111:{"eventId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","eventType":"OrderCreated","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":1977.3,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:00:00Z"}
```

OrderPaid:

```text
11111111-1111-1111-1111-111111111111:{"eventId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","eventType":"OrderPaid","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":1977.3,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:05:00Z"}
```

## 7. Verify ProjectionWorker

```powershell
docker logs microshop-projectionworker --tail 100
```

Look for:

```text
Projection event applied
EventType=OrderCreated
EventType=OrderPaid
```

## 8. Verify Read Model

Lite mode direct API:

```text
GET {{order_query_url}}/order-summaries
GET {{order_query_url}}/order-summaries/11111111-1111-1111-1111-111111111111
```

Full system or optional gateway mode:

```text
GET {{gateway_url}}/order-summaries
GET {{gateway_url}}/order-summaries/11111111-1111-1111-1111-111111111111
```

Expected important fields:

```text
status = Paid
lastProjectedEventType = OrderPaid
lastProjectedEventId = bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb
paidAtUtc has value
```

## 9. Check Kafka Lag

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Expected:

```text
LAG = 0
```

## 10. Verify Projection Failures

Produce invalid payload:

```text
33333333-3333-3333-3333-333333333333:{"bad":"payload"}
```

Expected:

```text
ProjectionWorker stores it in projection_failures.
Offset is committed.
Partition is not blocked by this invalid training message.
```

Check MongoDB:

```powershell
docker exec -it microshop-mongodb mongosh -u microshop -p microshop --authenticationDatabase admin
```

Inside Mongo shell:

```javascript
use MicroShop_OrderReadDb
db.projection_failures.find().sort({createdAtUtc:-1}).limit(5).pretty()
```

## 11. Explain Architecture

```text
RabbitMQ handles workflow/task messaging.
Kafka handles event stream/projection learning.
MongoDB stores the order read model.
OrderQueryService exposes read endpoints.
Kafka does not replace RabbitMQ.
OrderingService does not publish Kafka events yet.
```

## 12. Mention Limits Honestly

```text
No Kafka DLT yet.
No processed-event collection yet.
No projection rebuild command yet.
No OrderingService Kafka publisher yet.
No schema registry yet.
No production observability dashboard yet.
```
