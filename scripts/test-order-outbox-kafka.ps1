param(
    [string]$GatewayBaseUrl = "http://localhost:5027",
    [string]$KafkaContainer = "microshop-kafka",
    [string]$Topic = "microshop.order-events",
    [Guid]$CustomerId = [Guid]::NewGuid(),
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"

$checkout = @{ customerId = $CustomerId } | ConvertTo-Json
$headers = @{ "Idempotency-Key" = "outbox-kafka-$([Guid]::NewGuid().ToString('N'))" }

Write-Host "Checking Kafka topic '$Topic'..."
docker exec $KafkaContainer kafka-topics --bootstrap-server localhost:9092 --describe --topic $Topic | Out-Host

Write-Host "Submitting checkout. The basket for $CustomerId must already contain at least one item."
$response = Invoke-RestMethod -Method Post -Uri "$GatewayBaseUrl/orders/checkout" -Headers $headers -ContentType "application/json" -Body $checkout
$orderId = $response.id

if ([string]::IsNullOrWhiteSpace($orderId)) {
    throw "Checkout response did not contain an order id."
}

Write-Host "Order $orderId created. Waiting for the outbox -> Kafka -> Mongo projection flow..."
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$projectionUri = "$GatewayBaseUrl/order-summaries/$orderId"

while ((Get-Date) -lt $deadline) {
    try {
        $projection = Invoke-RestMethod -Method Get -Uri $projectionUri
        if ($projection.orderId -eq $orderId) {
            Write-Host "PASS: Order $orderId reached the MongoDB read model through Kafka."
            exit 0
        }
    }
    catch {
        # A 404 is expected until ProjectionWorker applies the Kafka event.
    }

    Start-Sleep -Seconds 2
}

throw "Order $orderId did not reach the read model within $TimeoutSeconds seconds. Inspect OrderingService outbox and ProjectionWorker logs."
