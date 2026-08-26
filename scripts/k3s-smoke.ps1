[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "https://api.example.com",
    [string]$StorefrontBaseUrl = "https://shop.example.com",
    [string]$OperationsBaseUrl = "https://ops.example.com",
    [int]$TimeoutSeconds = 180,
    [int]$PollSeconds = 3,
    [string]$EnvFile = ".env.k3s",
    [string]$AdminUserName,
    [string]$AdminPassword,
    [switch]$SkipAuth
)

$ErrorActionPreference = "Stop"

$GatewayBaseUrl = $GatewayBaseUrl.TrimEnd("/")

function Get-EnvFileValue {
    param([Parameter(Mandatory = $true)][string]$Key)

    if (-not (Test-Path -Path $EnvFile)) {
        return $null
    }

    foreach ($line in Get-Content -Path $EnvFile) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
            continue
        }

        $separatorIndex = $trimmed.IndexOf("=")
        if ($separatorIndex -lt 1) {
            continue
        }

        if ($trimmed.Substring(0, $separatorIndex).Trim() -eq $Key) {
            return $trimmed.Substring($separatorIndex + 1).Trim()
        }
    }

    return $null
}

function Wait-HttpOk {
    param([Parameter(Mandatory = $true)][string]$Url)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null

    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -UseBasicParsing -TimeoutSec 5

            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                Write-Host "[ok] $Url"
                return
            }

            $lastError = "HTTP $($response.StatusCode)"
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Seconds $PollSeconds
    }

    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Invoke-JsonGet {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [hashtable]$Headers
    )

    $parameters = @{
        Uri = "$GatewayBaseUrl$Path"
        Method = "Get"
        TimeoutSec = 10
    }

    if ($Headers) {
        $parameters.Headers = $Headers
    }

    $result = Invoke-RestMethod @parameters
    Write-Host "[ok] GET $Path"

    return $result
}

Write-Host "Running MicroShop K3s smoke against $GatewayBaseUrl"

Wait-HttpOk "$GatewayBaseUrl/alive"
Wait-HttpOk "$GatewayBaseUrl/health"
Wait-HttpOk "$StorefrontBaseUrl/"
Wait-HttpOk "$OperationsBaseUrl/"

Invoke-JsonGet "/catalog/products" | Out-Null
Invoke-JsonGet "/discounts/SAVE10" | Out-Null
Invoke-JsonGet "/order-summaries" | Out-Null

if (-not $SkipAuth) {
    if ([string]::IsNullOrWhiteSpace($AdminUserName)) {
        $AdminUserName = Get-EnvFileValue "MICROSHOP_BOOTSTRAP_ADMIN_USERNAME"
    }

    if ([string]::IsNullOrWhiteSpace($AdminPassword)) {
        $AdminPassword = Get-EnvFileValue "MICROSHOP_BOOTSTRAP_ADMIN_PASSWORD"
    }

    if ([string]::IsNullOrWhiteSpace($AdminUserName) -or [string]::IsNullOrWhiteSpace($AdminPassword)) {
        throw "Authentication smoke requires -AdminUserName/-AdminPassword or Bootstrap admin values in $EnvFile. Use -SkipAuth only when Identity is intentionally excluded."
    }

    $body = @{
        userName = $AdminUserName
        password = $AdminPassword
    } | ConvertTo-Json

    $login = Invoke-RestMethod -Uri "$GatewayBaseUrl/auth/login" -Method Post -ContentType "application/json" -Body $body -TimeoutSec 10

    if ([string]::IsNullOrWhiteSpace($login.accessToken)) {
        throw "Identity login did not return accessToken."
    }

    Invoke-RestMethod -Uri "$GatewayBaseUrl/auth/me" -Method Get -Headers @{ Authorization = "Bearer $($login.accessToken)" } -TimeoutSec 10 | Out-Null
    Write-Host "[ok] GET /auth/me"

    Invoke-JsonGet "/orders" -Headers @{ Authorization = "Bearer $($login.accessToken)" } | Out-Null
}
else {
    Write-Host "[skip] GET /orders requires authentication in K3s mode."
}

Write-Host "MicroShop K3s smoke passed."
