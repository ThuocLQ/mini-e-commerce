[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://localhost:5027",
    [int]$TimeoutSeconds = 180,
    [int]$PollSeconds = 3,
    [string]$EnvFile = ".env.local-prod",
    [string]$AdminUserName,
    [string]$AdminPassword,
    [switch]$SkipAuth,
    [switch]$SkipReadModel,
    [switch]$VerifyPortfolioFrontends,
    [string]$StorefrontBaseUrl = "http://localhost:5027",
    [string]$OperationsBaseUrl = "http://operations.localhost:5027"
)

$ErrorActionPreference = "Stop"

$GatewayBaseUrl = $GatewayBaseUrl.TrimEnd("/")
$StorefrontBaseUrl = $StorefrontBaseUrl.TrimEnd("/")
$OperationsBaseUrl = $OperationsBaseUrl.TrimEnd("/")

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
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url
    )

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

function Assert-PageContains {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedText
    )

    $response = Invoke-WebRequest -Uri $Url -Method Get -UseBasicParsing -TimeoutSec 10
    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "Expected $Url to return a success response, got HTTP $($response.StatusCode)."
    }

    if (-not $response.Content.Contains($ExpectedText)) {
        throw "Expected $Url to contain '$ExpectedText'."
    }

    Write-Host "[ok] UI $Url"
}

function Invoke-JsonGet {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $url = "$GatewayBaseUrl$Path"
    $result = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 10
    Write-Host "[ok] GET $Path"

    return $result
}

function Invoke-JsonPost {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [object]$Body
    )

    $url = "$GatewayBaseUrl$Path"
    $json = $Body | ConvertTo-Json -Depth 10
    $result = Invoke-RestMethod -Uri $url -Method Post -ContentType "application/json" -Body $json -TimeoutSec 10
    Write-Host "[ok] POST $Path"

    return $result
}

Write-Host "Running MicroShop local-prod smoke against $GatewayBaseUrl"

Wait-HttpOk "$GatewayBaseUrl/alive"
Wait-HttpOk "$GatewayBaseUrl/health"

$products = Invoke-JsonGet "/catalog/products"
$coupon = Invoke-JsonGet "/discounts/SAVE10"

if (-not $SkipReadModel) {
    $orderSummaries = Invoke-JsonGet "/order-summaries"
}

if ($null -eq $products) {
    throw "Catalog products response was empty."
}


if (-not $SkipReadModel -and $null -eq $orderSummaries) {
    throw "Order summaries response was empty."
}

if ($null -eq $coupon) {
    throw "Discount coupon response was empty."
}

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

    $login = Invoke-JsonPost "/auth/login" @{
        userName = $AdminUserName
        password = $AdminPassword
    }

    if ([string]::IsNullOrWhiteSpace($login.accessToken)) {
        throw "Identity login did not return accessToken."
    }

    Invoke-RestMethod `
        -Uri "$GatewayBaseUrl/auth/me" `
        -Method Get `
        -Headers @{ Authorization = "Bearer $($login.accessToken)" } `
        -TimeoutSec 10 | Out-Null

    Write-Host "[ok] GET /auth/me"

    $orders = Invoke-RestMethod `
        -Uri "$GatewayBaseUrl/orders" `
        -Method Get `
        -Headers @{ Authorization = "Bearer $($login.accessToken)" } `
        -TimeoutSec 10

    if ($null -eq $orders) {
        throw "Orders response was empty."
    }

    Write-Host "[ok] GET /orders"
}

if ($VerifyPortfolioFrontends) {
    Assert-PageContains -Url $StorefrontBaseUrl -ExpectedText "MicroShop"
    Assert-PageContains -Url $OperationsBaseUrl -ExpectedText "MicroShop Operations"
}

Write-Host "MicroShop local-prod smoke passed."
