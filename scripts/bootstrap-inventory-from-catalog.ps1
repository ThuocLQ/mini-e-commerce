[CmdletBinding()]
param(
    [string]$CatalogBaseUrl = "http://localhost:5045",
    [string]$InventoryBaseUrl = "http://localhost:5098",
    [string]$InternalApiKey = "microshop-development-internal-api-key"
)

$ErrorActionPreference = "Stop"

$catalogUrl = "$($CatalogBaseUrl.TrimEnd('/'))/products"
$products = Invoke-RestMethod -Method Get -Uri $catalogUrl

if ($null -eq $products) {
    throw "Catalog did not return a product list."
}

$headers = @{ "X-MicroShop-Internal-Key" = $InternalApiKey }
$processed = 0

foreach ($product in @($products)) {
    if ([string]::IsNullOrWhiteSpace($product.id)) {
        throw "Catalog returned a product without an id."
    }

    if ($product.stockQuantity -lt 0) {
        throw "Catalog product '$($product.id)' has a negative stock quantity."
    }

    $uri = "$($InventoryBaseUrl.TrimEnd('/'))/_internal/inventory/items/$($product.id)/stock"
    $body = @{ stockQuantity = [int]$product.stockQuantity } | ConvertTo-Json -Compress
    Invoke-RestMethod -Method Put -Uri $uri -Headers $headers -ContentType "application/json" -Body $body | Out-Null
    $processed++
}

Write-Host "Bootstrapped $processed inventory items from Catalog."
