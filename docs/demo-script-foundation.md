# MicroShop Foundation Demo Script

## 1. Start Lite Projection Demo

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker api-gateway
```

## 2. Start Full System

Use this when demoing all services:

```powershell
docker compose up -d --build
```

## 3. Verify Health

Check:

```text
GET /health
```

Services:

```text
ApiGateway
OrderQueryService
```

`ApiGateway` maps `/health` explicitly for Docker.

## 4. Create Kafka Topic

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
```

## 5. Produce Demo Order Event

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

Payload:

```text
11111111-1111-1111-1111-111111111111:{"eventId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","eventType":"OrderCreated","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":1977.3,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:00:00Z"}
```

## 6. Verify ProjectionWorker

```powershell
docker logs microshop-projectionworker --tail 100
```

Look for:

```text
Projection event applied
```

## 7. Verify Read Model

```text
GET /order-summaries/11111111-1111-1111-1111-111111111111
```

Expected:

```text
status = Created
lastProjectedEventType = OrderCreated
```

## 8. Check Kafka Lag

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Expected:

```text
LAG = 0
```

## 9. Explain Architecture

```text
RabbitMQ handles workflow/task messaging.
Kafka handles event stream/projection learning.
MongoDB stores the order read model.
OrderQueryService exposes read endpoints.
```

## 10. Mention Limits Honestly

```text
No Kafka DLT yet.
No projection rebuild command yet.
No OrderingService Kafka publisher yet.
No production observability dashboard yet.
```
