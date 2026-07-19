[CmdletBinding()]
param(
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

function Invoke-Docker {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    & docker @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker command failed: docker $($Arguments -join ' ')"
    }
}

function Send-Event {
    param(
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$Json
    )

    "$Key`:$Json" | docker exec -i microshop-kafka kafka-console-producer `
        --bootstrap-server localhost:9092 `
        --topic microshop.order-events `
        --property parse.key=true `
        --property key.separator=:

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish projection test event."
    }
}

function Wait-MongoCount {
    param(
        [Parameter(Mandatory = $true)][string]$Collection,
        [Parameter(Mandatory = $true)][string]$Field,
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][long]$Expected
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $expression = "db.getSiblingDB('MicroShop_OrderReadDb').getCollection('$Collection').countDocuments({${Field}:'$Value'})"

    while ((Get-Date) -lt $deadline) {
        $output = & docker exec microshop-mongodb mongosh --quiet `
            --username microshop `
            --password microshop `
            --authenticationDatabase admin `
            --eval $expression

        $actual = 0L
        $parsed = [long]::TryParse(
            ($output | Select-Object -Last 1),
            [ref]$actual)
        if ($LASTEXITCODE -eq 0 -and $parsed -and $actual -eq $Expected) {
            return
        }

        Start-Sleep -Seconds 2
    }

    throw "MongoDB collection '$Collection' did not reach count $Expected for $Field=$Value."
}

function Wait-DeadLetter {
    param([Parameter(Mandatory = $true)][string]$Marker)

    $group = "projection-dlt-smoke-$([Guid]::NewGuid().ToString('N'))"
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            $output = & docker exec microshop-kafka kafka-console-consumer `
                --bootstrap-server localhost:9092 `
                --topic microshop.order-events.dlt `
                --group $group `
                --from-beginning `
                --timeout-ms 5000 2>$null
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }

        if (($output | Out-String) -like "*$Marker*") {
            return
        }
    }

    throw "DLT did not receive the invalid projection event marker '$Marker'."
}

Write-Host "Starting Kafka, MongoDB, topic initializer, and ProjectionWorker..."
Invoke-Docker -Arguments @(
    "compose", "up", "-d", "--build",
    "zookeeper", "kafka", "kafka-init", "mongodb", "projectionworker"
)

$eventId = [Guid]::NewGuid()
$orderId = [Guid]::NewGuid()
$customerId = [Guid]::NewGuid()
$productId = [Guid]::NewGuid()
$occurredAtUtc = [DateTime]::UtcNow
$orderIdText = $orderId.ToString("D")
$eventIdText = $eventId.ToString("D")

$validEvent = @{
    eventId = $eventId
    eventType = "OrderCreated"
    orderId = $orderId
    customerId = $customerId
    customerName = "Projection Reliability Smoke"
    totalAmount = 125000
    currency = "VND"
    itemCount = 1
    items = @(
        @{
            productId = $productId
            productName = "Smoke Product"
            quantity = 1
            unitPrice = 125000
        }
    )
    occurredAtUtc = $occurredAtUtc
} | ConvertTo-Json -Depth 8 -Compress

Write-Host "Publishing the same EventId twice..."
Send-Event -Key $orderIdText -Json $validEvent
Send-Event -Key $orderIdText -Json $validEvent

Wait-MongoCount -Collection "order_summaries" -Field "orderId" -Value $orderIdText -Expected 1
Wait-MongoCount -Collection "processed_projection_events" -Field "eventId" -Value $eventIdText -Expected 1
Write-Host "[ok] Duplicate event produced one read model and one processed marker."

$invalidMarker = "unsupported-$([Guid]::NewGuid().ToString('N'))"
$invalidOrderId = [Guid]::NewGuid()
$invalidEvent = @{
    eventId = [Guid]::NewGuid()
    eventType = "UnsupportedOrderEvent"
    orderId = $invalidOrderId
    customerId = [Guid]::NewGuid()
    customerName = $invalidMarker
    totalAmount = 1
    currency = "VND"
    itemCount = 0
    items = @()
    occurredAtUtc = [DateTime]::UtcNow
} | ConvertTo-Json -Depth 8 -Compress

Write-Host "Publishing a permanent invalid event..."
Send-Event -Key ($invalidOrderId.ToString("D")) -Json $invalidEvent
Wait-DeadLetter -Marker $invalidMarker
Write-Host "[ok] Invalid event was routed to microshop.order-events.dlt."

Write-Host "Projection reliability smoke passed."
