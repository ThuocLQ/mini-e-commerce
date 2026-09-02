[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://api.localhost:5027"
)

$ErrorActionPreference = "Stop"

$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 10)
$userName = "notification-pref-$suffix"
$password = "NotificationPreferenceSmoke!2026"
$email = "$userName@microshop.test"

Invoke-RestMethod -Method Post -Uri "$GatewayBaseUrl/auth/register" -ContentType "application/json" -Body (
    @{ userName = $userName; email = $email; password = $password } | ConvertTo-Json
) | Out-Null

$login = Invoke-RestMethod -Method Post -Uri "$GatewayBaseUrl/auth/login" -ContentType "application/json" -Body (
    @{ userName = $userName; password = $password } | ConvertTo-Json
)

$headers = @{ Authorization = "Bearer $($login.accessToken)" }
$before = Invoke-RestMethod -Uri "$GatewayBaseUrl/me/notification-preferences" -Headers $headers
$after = Invoke-RestMethod -Method Put -Uri "$GatewayBaseUrl/me/notification-preferences" -Headers (
    $headers + @{ "Content-Type" = "application/json" }
) -Body (@{ receiveOrderUpdates = $false } | ConvertTo-Json)
$me = Invoke-RestMethod -Uri "$GatewayBaseUrl/auth/me" -Headers $headers

if (-not $before.receiveOrderUpdates -or $after.receiveOrderUpdates -or $me.receiveOrderUpdates) {
    throw "Notification preference did not persist opt-out through the Identity and Gateway contracts."
}

Write-Host "Notification preference smoke passed. UserName=$userName"