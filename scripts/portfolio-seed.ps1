[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://api.localhost:5027",
    [string]$EnvFile = ".env.local-prod",
    [int]$InventoryPropagationWaitSeconds = 8
)

$ErrorActionPreference = "Stop"
$GatewayBaseUrl = $GatewayBaseUrl.TrimEnd("/")

function Get-EnvFileValue {
    param([Parameter(Mandatory = $true)][string]$Key)

    if (-not (Test-Path -Path $EnvFile)) {
        throw "Missing $EnvFile."
    }

    foreach ($line in Get-Content -Path $EnvFile) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
            continue
        }

        $separatorIndex = $trimmed.IndexOf("=")
        if ($separatorIndex -gt 0 -and $trimmed.Substring(0, $separatorIndex).Trim() -eq $Key) {
            return $trimmed.Substring($separatorIndex + 1).Trim()
        }
    }

    return $null
}

$adminUserName = Get-EnvFileValue "MICROSHOP_BOOTSTRAP_ADMIN_USERNAME"
$adminPassword = Get-EnvFileValue "MICROSHOP_BOOTSTRAP_ADMIN_PASSWORD"
if ([string]::IsNullOrWhiteSpace($adminUserName) -or [string]::IsNullOrWhiteSpace($adminPassword)) {
    throw "Portfolio seed requires MICROSHOP_BOOTSTRAP_ADMIN_USERNAME and MICROSHOP_BOOTSTRAP_ADMIN_PASSWORD in $EnvFile."
}

$loginBody = @{ userName = $adminUserName; password = $adminPassword } | ConvertTo-Json -Compress
$login = Invoke-RestMethod -Method Post -Uri "$GatewayBaseUrl/auth/login" -ContentType "application/json" -Body $loginBody -TimeoutSec 15
if ([string]::IsNullOrWhiteSpace($login.accessToken)) {
    throw "Portfolio seed could not obtain an admin access token."
}

$headers = @{ Authorization = "Bearer $($login.accessToken)" }
$products = @((Invoke-WebRequest -UseBasicParsing -Method Get -Uri "$GatewayBaseUrl/catalog/products" -TimeoutSec 15).Content | ConvertFrom-Json)
$demoProducts = @(
    @{ Name = "Aurora Wireless Headphones"; Description = "Over-ear headphones with adaptive noise control and 40-hour battery life."; Price = [decimal]149.00; StockQuantity = 24 },
    @{ Name = "Orbit Mechanical Keyboard"; Description = "Compact wireless keyboard with tactile switches and multi-device pairing."; Price = [decimal]109.00; StockQuantity = 18 },
    @{ Name = "Field Notes Desk Set"; Description = "Everyday desk organizer with notebook, pen, cable clips and storage tray."; Price = [decimal]39.00; StockQuantity = 35 },
    @{ Name = "Atlas USB-C Hub"; Description = "Seven-port USB-C hub with HDMI, Ethernet and power delivery passthrough."; Price = [decimal]79.00; StockQuantity = 20 }
)

foreach ($demoProduct in $demoProducts) {
    $existing = @($products | Where-Object { $_.name -eq $demoProduct.Name }) | Select-Object -First 1
    $product = $existing

    if ($null -eq $product) {
        $body = $demoProduct | ConvertTo-Json -Compress
        $product = Invoke-RestMethod -Method Post -Uri "$GatewayBaseUrl/catalog/products" -Headers $headers -ContentType "application/json" -Body $body -TimeoutSec 15
        Write-Host "[created] $($product.name)"
    }
    else {
        Write-Host "[present] $($product.name)"
    }

    $stockBody = @{ stockQuantity = $demoProduct.StockQuantity } | ConvertTo-Json -Compress
    Invoke-RestMethod -Method Put -Uri "$GatewayBaseUrl/catalog/products/$($product.id)/stock" -Headers $headers -ContentType "application/json" -Body $stockBody -TimeoutSec 15 | Out-Null
    Write-Host "[inventory synchronized] $($product.name)"
}

if ($InventoryPropagationWaitSeconds -gt 0) {
    Start-Sleep -Seconds $InventoryPropagationWaitSeconds
}

Write-Host "Portfolio catalog seed completed."