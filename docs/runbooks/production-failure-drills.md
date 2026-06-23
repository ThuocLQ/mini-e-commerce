# MicroShop Production Failure Drills

This runbook is the practical checklist for the Day 50 senior production demo.

The goal is not to prove MicroShop is production-ready. The goal is to prove the project can explain and exercise production failure behavior.

## Preconditions

```powershell
docker compose up -d --build
docker compose ps
```

Expected baseline:

```text
api-gateway, catalogservice, basketservice, orderingservice, paymentservice,
orderqueryservice, projectionworker, notificationworker, postgres, redis,
rabbitmq, kafka, mongodb are running or healthy.
```

## Drill 1: Gateway Rate Limit

Target:

```text
GET http://localhost:5027/catalog/products
```

Run repeated requests until the gateway returns:

```text
429 Too Many Requests
```

Expected:

```text
ApiGateway remains healthy.
Downstream services are not called without limit.
Response contains security headers such as X-Content-Type-Options and X-Frame-Options.
```

## Drill 2: Basket -> Catalog Downstream Failure

Stop CatalogService:

```powershell
docker compose stop catalogservice
```

Call a Basket endpoint that depends on Catalog:

```text
GET http://localhost:5027/cart/products/{productId}/validate
```

Expected:

```text
BasketService does not hang for a long time.
Response is controlled, usually 503 DOWNSTREAM_UNAVAILABLE.
Catalog REST client uses timeout/retry/circuit-breaker policy.
```

Restart:

```powershell
docker compose start catalogservice
```

## Drill 3: Duplicate Payment Webhook

Send the same webhook payload twice with the same `providerEventId` and valid `X-MicroShop-Signature`.

Expected:

```text
PaymentService records duplicate safely.
PaymentOutboxMessages does not get a second business event for the same provider event.
OrderingService saga remains idempotent.
```

## Drill 4: Late Payment Success Compensation

Create or use an order that is already cancelled or timed out, then dispatch a `PaymentSucceeded` event for that order.

Expected:

```text
OrderingService does not mark the order Paid blindly.
OrderPaymentSaga moves to CompensationRequired.
```

## Drill 5: RabbitMQ / Notification Failure

Stop RabbitMQ or NotificationWorker:

```powershell
docker compose stop notificationworker
```

Create an order through checkout.

Expected:

```text
OrderingService stores order and outbox in the same local transaction.
Outbox remains pending/failed until the dispatcher can publish.
OrderingService does not lose the business event.
```

Restart:

```powershell
docker compose start notificationworker
```

## Drill 6: Kafka Projection Lag

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
