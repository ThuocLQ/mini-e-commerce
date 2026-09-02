[CmdletBinding()]
param(
    [string]$CatalogCsvPath = "data/portfolio/catalog-products.csv",
    [int]$TimeoutSeconds = 15
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $CatalogCsvPath)) {
    throw "Catalog CSV not found: $CatalogCsvPath"
}

$products = @(Import-Csv -LiteralPath $CatalogCsvPath)
if ($products.Count -lt 12) {
    throw "Portfolio catalog must contain at least 12 curated products; found $($products.Count)."
}

$requiredColumns = @("Sku", "Name", "Description", "Price", "StockQuantity", "Category", "Brand", "ImageUrl")
foreach ($column in $requiredColumns) {
    if ($products[0].PSObject.Properties.Name -notcontains $column) {
        throw "Catalog CSV is missing required column '$column'."
    }
}

$duplicateSku = @($products | Group-Object Sku | Where-Object Count -gt 1 | Select-Object -First 1)
if ($duplicateSku) {
    throw "Catalog CSV contains duplicate SKU '$($duplicateSku.Name)'."
}

$categories = @($products | ForEach-Object { "$($_.Category)".Trim() } | Where-Object { $_ } | Sort-Object -Unique)
if ($categories.Count -lt 4) {
    throw "Portfolio catalog needs at least four customer-selectable categories; found $($categories.Count)."
}

$failures = @()
foreach ($product in $products) {
    $sku = "$($product.Sku)".Trim()
    $name = "$($product.Name)".Trim()
    $description = "$($product.Description)".Trim()
    $imageUrl = "$($product.ImageUrl)".Trim()

    if (-not $sku -or -not $name -or -not $description -or -not $imageUrl) {
        $failures += "$sku is missing required storefront content."
        continue
    }

    [Uri]$uri = $null
    if (-not [Uri]::TryCreate($imageUrl, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @("https", "http")) {
        $failures += "$sku has an invalid ImageUrl."
        continue
    }

    try {
        $response = Invoke-WebRequest -UseBasicParsing -Method Head -Uri $uri -TimeoutSec $TimeoutSeconds
        if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) {
            $failures += "$sku image returned HTTP $($response.StatusCode)."
        }
    }
    catch {
        $failures += "$sku image could not be reached: $($_.Exception.Message)"
    }
}

if ($failures.Count -gt 0) {
    throw ("Catalog media validation failed:`n - " + ($failures -join "`n - "))
}

[PSCustomObject]@{
    ProductCount = $products.Count
    Categories = $categories -join ", "
    VerifiedImages = $products.Count
} | Format-List

Write-Host "Portfolio catalog media validation passed."