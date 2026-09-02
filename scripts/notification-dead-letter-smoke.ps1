[CmdletBinding()]
param(
    [string]$ComposeFile = "compose.local-prod.yml",
    [string]$EnvFile = ".env.local-prod",
    [int]$WaitSeconds = 20
)

$ErrorActionPreference = "Stop"

function Invoke-Compose {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    & docker compose --env-file $EnvFile -f $ComposeFile -f compose.portfolio.yml @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$eventId = [Guid]::NewGuid()
$deliveryId = [Guid]::NewGuid()
$customerId = [Guid]::NewGuid()

$insert = @"
INSERT INTO NotificationDeliveries (
    Id, EventId, EventType, Template, Channel, CustomerId,
    Status, AttemptCount, LastError, CreatedAtUtc, UpdatedAtUtc)
VALUES (
    '$deliveryId', '$eventId', 'microshop.smoke.failure', 'dead-letter-smoke', 'email', '$customerId',
    'RetryableFailure', 4, 'simulated smtp failure', NOW() - INTERVAL '1 minute', NOW() - INTERVAL '1 minute');
"@

try {
    Invoke-Compose exec -T postgres psql -U microshop -d notificationdb -c $insert
    Start-Sleep -Seconds $WaitSeconds

    $status = @(
        Invoke-Compose exec -T postgres psql -U microshop -d notificationdb -At -c "SELECT Status FROM NotificationDeliveries WHERE EventId = '$eventId';" |
            Where-Object { $_ -match '\S' }
    ) | Select-Object -Last 1

    if ($status -ne "DeadLetter") {
        throw "Expected DeadLetter, received '$status'. Ensure NotificationWorker is running with recovery enabled."
    }

    Write-Host "Notification dead-letter smoke passed. EventId=$eventId"
}
finally {
    Invoke-Compose exec -T postgres psql -U microshop -d notificationdb -c "DELETE FROM NotificationDeliveries WHERE EventId = '$eventId';" | Out-Null
}