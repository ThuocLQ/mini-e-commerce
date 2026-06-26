# MicroShop Production Failure Drills

This runbook is the Day 50 production-minded demo script.

The goal is not to claim MicroShop is fully production-ready. The goal is to prove the system can be operated, broken deliberately, observed, recovered, and explained.

## Preconditions

Start the full local runtime:

```powershell
docker compose up -d --build
docker compose ps
```

Expected baseline:

```text
postgres, redis, rabbitmq, zookeeper, kafka, mongodb,
catalogservice, basketservice, orderingservice, discountservice,
identityservice, paymentservice, orderqueryservice, projectionworker,
notificationworker, api-gateway are running or healthy.
```

Use the gateway base URL:

```text
http://localhost:5027
```

Optional evidence commands:

```powershell
docker compose logs --tail=80 api-gateway
docker compose logs --tail=80 orderingservice
docker compose logs --tail=80 paymentservice
docker compose logs --tail=80 projectionworker
```

## Drill 1: Gateway Rate Limit

Purpose:

```text
prove the edge rejects abusive traffic before downstream services do unlimited work.
```

Run `MicroShop - Day 50 Production Failure Drills / 01 - Gateway rate limit probe` in Postman Runner with a high iteration count, or run repeated requests manually:

```powershell
1..120 | ForEach-Object { Invoke-WebRequest http://localhost:5027/catalog/products -SkipCertificateCheck -ErrorAction SilentlyContinue | Select-Object StatusCode }
```

Expected:

```text
429 Too Many Requests
ApiGateway remains healthy.
Response includes security headers such as X-Content-Type-Options and X-Frame-Options.
```

Evidence:

```text
HTTP status codes
api-gateway logs
Prometheus/Grafana request rate if observability profile is running
```

## Drill 2: Basket -> Catalog Downstream Failure

Purpose:

```text
prove a downstream dependency failure returns a controlled API response instead of a long hang.
```

Stop CatalogService:

```powershell
docker compose stop catalogservice
```

Call:

```text
GET http://localhost:5027/cart/products/{productId}/validate
```

Expected:

```text
BasketService does not hang for a long time.
Response is controlled, usually 503 Service Unavailable.
No basket mutation is performed.
```

Restart:

```powershell
docker compose start catalogservice
```

## Drill 3: Duplicate Payment Webhook

Purpose:

```text
prove provider retries do not create duplicate business events.
```

Run these Postman requests:

```text
MicroShop - Day 50 Production Failure Drills
03 - Create payment for duplicate webhook
04 - Send signed succeeded webhook
05 - Send duplicate signed webhook
06 - Get payment after duplicate
```

The collection signs the webhook with the local shared secret `dev-webhook-secret`.

Expected:

```text
PaymentService records duplicate safely.
PaymentOutboxMessages does not get a second business event for the same provider event.
OrderingService saga remains idempotent.
```

## Drill 4: Late Payment Success Compensation

Purpose:

```text
prove a late payment success does not blindly mark a cancelled order as paid.
```

Dispatch a payment event to a cancelled/timed-out order:

```text
POST http://localhost:5027/orders/{orderId}/payment-events
```

Body:

```json
{
  "eventType": "PaymentSucceeded",
  "paymentId": "55555555-5555-5555-5555-555555555555",
  "amount": 100,
  "currency": "USD"
}
```

Expected:

```text
OrderingService does not mark the order Paid blindly.
OrderPaymentSaga moves to CompensationRequired.
```

## Drill 5: RabbitMQ / Notification Failure

Purpose:

```text
prove OrderingService does not lose integration events when a consumer is down.
```

Stop NotificationWorker:

```powershell
docker compose stop notificationworker
```

Create an order through checkout. If you use Postman, run the normal full system flow up to checkout, then inspect outbox:

```text
GET http://localhost:5028/debug/outbox
```

This debug endpoint is intentionally checked directly on `OrderingService`. The Docker gateway blocks debug routes outside `Development`.

Expected:

```text
OrderingService stores order and outbox in the same local transaction.
Outbox remains pending/failed until the dispatcher can publish.
OrderingService does not lose the business event.
```

Restart and verify recovery:

```powershell
docker compose start notificationworker
docker compose logs --tail=80 notificationworker
```

## Drill 6: Kafka Projection Lag

Purpose:

```text
prove the read model projection can catch up from Kafka lag.
```

Produce several events to `microshop.order-events`, then inspect lag:

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Expected:

```text
Lag eventually returns to 0.
Invalid messages are stored in projection_failures and do not block the partition forever.
```

## Drill 7: MongoDB Projection Failure

Purpose:

```text
prove the projection worker does not commit offsets before MongoDB is updated.
```

Stop MongoDB while ProjectionWorker is consuming:

```powershell
docker compose stop mongodb
```

Expected:

```text
ProjectionWorker does not commit offsets after MongoDB write failure.
After MongoDB returns, replay/upsert keeps the read model idempotent.
```

Restart:

```powershell
docker compose start mongodb
```

## Evidence To Capture

For each drill, capture:

```text
command run
HTTP status / response
relevant service logs
queue/topic/DB state
expected vs actual result
follow-up gap if behavior is still incomplete
```

## Demo Close-Out

Use this short narrative when presenting the project:

```text
MicroShop is still a learning system, but it now has production-style failure surfaces:
gateway protection, correlation IDs, metrics, outbox-backed messaging, idempotent webhooks,
projection replay tests, and explicit failure drills. The remaining production work is CI/CD,
local-prod compose, secrets hygiene, backup/restore, and stronger event contracts.
```
