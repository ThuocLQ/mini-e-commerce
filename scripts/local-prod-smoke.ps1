[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://localhost:5027",
    [int]$TimeoutSeconds = 180,
    [int]$PollSeconds = 3,
    [switch]$SkipAuth
)

$ErrorActionPreference = "Stop"

$GatewayBaseUrl = $GatewayBaseUrl.TrimEnd("/")

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
$orders = Invoke-JsonGet "/orders"
$orderSummaries = Invoke-JsonGet "/order-summaries"
$coupon = Invoke-JsonGet "/discounts/SAVE10"

if ($null -eq $products) {
    throw "Catalog products response was empty."
}

if ($null -eq $orders) {
    throw "Orders response was empty."
}

if ($null -eq $orderSummaries) {
    throw "Order summaries response was empty."
}

if ($null -eq $coupon) {
    throw "Discount coupon response was empty."
}

if (-not $SkipAuth) {
    $login = Invoke-JsonPost "/auth/login" @{
        userName = "admin"
        password = "Admin@123"
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
}

Write-Host "MicroShop local-prod smoke passed."
