[CmdletBinding()]
param(
    [string]$ComposeFile = "compose.local-prod.yml",
    [string]$EnvFile = ".env.local-prod",
    [string]$GatewayBaseUrl = "http://api.localhost:5027",
    [string]$MailpitBaseUrl = "http://127.0.0.1:8025",
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"

function Invoke-Compose {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    & docker compose --env-file $EnvFile -f $ComposeFile -f compose.portfolio.yml @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 10)
$email = "notification-smoke-$suffix@microshop.test"
$registration = @{
    userName = "notification$suffix"
    email = $email
    password = "NotificationSmoke!2026"
} | ConvertTo-Json

$response = Invoke-RestMethod -Method Post -Uri "$GatewayBaseUrl/auth/register" -ContentType "application/json" -Body $registration

$deadline = (Get-Date).ToUniversalTime().AddSeconds($TimeoutSeconds)
$delivery = $null
$mailDelivered = $false

while ((Get-Date).ToUniversalTime() -lt $deadline) {
    $query = "SELECT Status || '|' || AttemptCount || '|' || EventId FROM NotificationDeliveries WHERE CustomerId = '$($response.userId)' ORDER BY CreatedAtUtc DESC LIMIT 1;"
    $delivery = @(Invoke-Compose exec -T postgres psql -U microshop -d notificationdb -At -c $query | Where-Object { $_ -match '\S' }) | Select-Object -Last 1

    $mailpit = Invoke-RestMethod -Uri "$MailpitBaseUrl/api/v1/messages"
    $mailDelivered = @(
        $mailpit.messages | Where-Object {
            @($_.To | Where-Object { $_.Address -eq $email }).Count -gt 0
        }
    ).Count -gt 0

    if ($delivery -like "Sent|1|*" -and $mailDelivered) {
        break
    }

    Start-Sleep -Seconds 1
}

if ([string]::IsNullOrWhiteSpace($delivery)) {
    throw "No NotificationDelivery row was created for customer '$($response.userId)'."
}

$parts = $delivery.Split('|')
if ($parts.Count -ne 3 -or $parts[0] -ne "Sent" -or [int]$parts[1] -ne 1) {
    throw "Expected one sent delivery attempt, received '$delivery'."
}

if (-not $mailDelivered) {
    throw "Mailpit did not receive verification mail for '$email'."
}

Write-Host "Notification delivery smoke passed. EventId=$($parts[2]) CustomerId=$($response.userId)"