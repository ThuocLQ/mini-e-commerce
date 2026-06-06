# MicroShop Local Debug Runbook

## Start Runtime

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker api-gateway
```

## Check Containers

```powershell
docker compose ps
```

Expected important containers:

```text
microshop-kafka
microshop-mongodb
microshop-projectionworker
microshop-orderqueryservice
microshop-api-gateway
```

## Check Kafka Topic

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --describe --topic microshop.order-events
```

If missing:

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
```

## Produce Valid Event

Start a keyed producer:

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

Paste:

```text
11111111-1111-1111-1111-111111111111:{"eventId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","eventType":"OrderCreated","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":1977.3,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:00:00Z"}
```

## Check ProjectionWorker Logs

```powershell
docker logs microshop-projectionworker --tail 100
```

Look for:

```text
Projection event applied
Projection message stored as failure
Projection MongoDB apply failed
```

## Check Consumer Group Lag

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Healthy expectation:

```text
LAG eventually becomes 0.
```

## Check Read API

Direct OrderQueryService:

```text
GET /order-summaries
GET /order-summaries/11111111-1111-1111-1111-111111111111
```

Via ApiGateway:

```text
GET /order-summaries
GET /order-summaries/11111111-1111-1111-1111-111111111111
```

## Check Health

OrderQueryService:

```text
GET /health
GET /alive
```

ApiGateway:

```text
GET /health
```

## Check MongoDB Manually

```powershell
docker exec -it microshop-mongodb mongosh -u microshop -p microshop --authenticationDatabase admin
```

```javascript
use MicroShop_OrderReadDb
db.order_summaries.find().pretty()
db.projection_failures.find().sort({ createdAtUtc: -1 }).limit(5).pretty()
```

## Common Issues

### ApiGateway /health Not Found In Docker

Cause:

```text
MapDefaultEndpoints only exposes default health endpoints in Development.
ApiGateway runs as ASPNETCORE_ENVIRONMENT=Docker in docker-compose.
```

Expected fix:

```text
ApiGateway maps /health explicitly.
```

### Order Summary Is Not Created

Check:

```text
Payload has customerName?
Payload has itemCount?
Payload has items?
orderId/customerId are GUID?
Kafka key equals orderId?
ProjectionWorker logs show success?
Message went to projection_failures?
```

### Lag Grows

Check:

```text
ProjectionWorker running?
MongoDB healthy?
Payload valid?
Poison message?
Consumer group id correct?
```

### Invalid Message

Expected:

```text
Invalid/unsupported messages are stored in projection_failures and committed.
```

### MongoDB Failure

Expected:

```text
MongoDB failure should not commit Kafka offset.
```
