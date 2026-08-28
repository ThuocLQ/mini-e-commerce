[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://api.localhost:5027",
    [string]$EnvFile = ".env.local-prod",
    [string]$CatalogCsvPath = "data/portfolio/catalog-products.csv",
    [int]$InventoryPropagationWaitSeconds = 8,
    [switch]$PruneLegacyDuplicates
)

$ErrorActionPreference = "Stop"
$GatewayBaseUrl = $GatewayBaseUrl.TrimEnd("/")

function Get-EnvFileValue {
    param([Parameter(Mandatory = $true)][string]$Key)

    if (-not (Test-Path -LiteralPath $EnvFile)) {
        throw "Missing $EnvFile."
    }

    foreach ($line in Get-Content -LiteralPath $EnvFile) {
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

function Require-CatalogColumns {
    param([Parameter(Mandatory = $true)][object[]]$Rows)

    $requiredColumns = @("Sku", "Name", "Description", "Price", "StockQuantity", "Category", "Brand", "ImageUrl")
    if ($Rows.Count -eq 0) {
        throw "Catalog CSV has no product rows."
    }

    $columns = @($Rows[0].PSObject.Properties.Name)
    foreach ($column in $requiredColumns) {
        if ($columns -notcontains $column) {
            throw "Catalog CSV is missing required column '$column'."
        }
    }
}

function Convert-CatalogRow {
    param([Parameter(Mandatory = $true)]$Row, [Parameter(Mandatory = $true)][int]$RowNumber)

    $sku = "$($Row.Sku)".Trim()
    $name = "$($Row.Name)".Trim()
    $description = "$($Row.Description)".Trim()
    $category = "$($Row.Category)".Trim()
    $brand = "$($Row.Brand)".Trim()
    $imageUrl = "$($Row.ImageUrl)".Trim()

    if ([string]::IsNullOrWhiteSpace($sku) -or [string]::IsNullOrWhiteSpace($name)) {
        throw "Catalog CSV row $RowNumber requires Sku and Name."
    }

    [decimal]$price = 0
    [int]$stockQuantity = 0
    if (-not [decimal]::TryParse("$($Row.Price)", [Globalization.NumberStyles]::Number, [Globalization.CultureInfo]::InvariantCulture, [ref]$price) -or $price -lt 0) {
        throw "Catalog CSV row $RowNumber has invalid Price. Use a non-negative decimal with '.' as the separator."
    }

    if (-not [int]::TryParse("$($Row.StockQuantity)", [ref]$stockQuantity) -or $stockQuantity -lt 0) {
        throw "Catalog CSV row $RowNumber has invalid StockQuantity."
    }

    [Uri]$parsedImageUrl = $null
    if (-not [Uri]::TryCreate($imageUrl, [UriKind]::Absolute, [ref]$parsedImageUrl) -or ($imageUrl -notmatch '^https?://')) {
        throw "Catalog CSV row $RowNumber has an invalid ImageUrl."
    }

    return [pscustomobject]@{
        Sku = $sku
        Name = $name
        Description = $description
        Price = $price
        StockQuantity = $stockQuantity
        Category = if ($category) { $category } else { $null }
        Brand = if ($brand) { $brand } else { $null }
        ImageUrl = $imageUrl
    }
}

$adminUserName = Get-EnvFileValue "MICROSHOP_BOOTSTRAP_ADMIN_USERNAME"
$adminPassword = Get-EnvFileValue "MICROSHOP_BOOTSTRAP_ADMIN_PASSWORD"
if ([string]::IsNullOrWhiteSpace($adminUserName) -or [string]::IsNullOrWhiteSpace($adminPassword)) {
    throw "Portfolio seed requires MICROSHOP_BOOTSTRAP_ADMIN_USERNAME and MICROSHOP_BOOTSTRAP_ADMIN_PASSWORD in $EnvFile."
}

if (-not (Test-Path -LiteralPath $CatalogCsvPath)) {
    throw "Catalog CSV not found: $CatalogCsvPath"
}

$rows = @(Import-Csv -LiteralPath $CatalogCsvPath)
Require-CatalogColumns -Rows $rows
$catalog = for ($index = 0; $index -lt $rows.Count; $index++) { Convert-CatalogRow -Row $rows[$index] -RowNumber ($index + 2) }

$duplicateSku = $catalog | Group-Object Sku | Where-Object Count -gt 1 | Select-Object -First 1
if ($duplicateSku) {
    throw "Catalog CSV has duplicate SKU '$($duplicateSku.Name)'."
}

$loginBody = @{ userName = $adminUserName; password = $adminPassword } | ConvertTo-Json -Compress
$login = Invoke-RestMethod -Method Post -Uri "$GatewayBaseUrl/auth/login" -ContentType "application/json" -Body $loginBody -TimeoutSec 15
if ([string]::IsNullOrWhiteSpace($login.accessToken)) {
    throw "Portfolio seed could not obtain an admin access token."
}

$headers = @{ Authorization = "Bearer $($login.accessToken)" }
$productsResponse = Invoke-RestMethod -Method Get -Uri "$GatewayBaseUrl/catalog/products" -TimeoutSec 15
$products = @(Write-Output $productsResponse)
$productsBySku = @{}
foreach ($product in $products) {
    if (-not [string]::IsNullOrWhiteSpace($product.sku)) {
        $productsBySku[$product.sku] = $product
    }
}

foreach ($product in $catalog) {
    $existing = $productsBySku[$product.Sku]
    $legacyDuplicates = @()

    if ($null -eq $existing) {
        $sameName = @($products | Where-Object { $_.name -eq $product.Name })
        $exactDescription = @($sameName | Where-Object { $_.description -eq $product.Description })
        if ($exactDescription.Count -eq 1) {
            $existing = $exactDescription[0]
            $legacyDuplicates = @($sameName | Where-Object { $_.id -ne $existing.id -and $_.sku -like 'LEGACY-*' })
        }
        elseif ($sameName.Count -eq 1) {
            $existing = $sameName[0]
        }
        elseif ($sameName.Count -gt 1) {
            throw "Catalog contains multiple products named '$($product.Name)' without an exact CSV description match. Resolve this manually before import."
        }
    }

    if ($null -eq $existing) {
        $body = $product | Select-Object Name, Price, Description, StockQuantity, Category, ImageUrl, Sku, Brand | ConvertTo-Json -Compress
        $existing = Invoke-RestMethod -Method Post -Uri "$GatewayBaseUrl/catalog/products" -Headers $headers -ContentType "application/json" -Body $body -TimeoutSec 15
        Write-Host "[created] $($existing.sku) - $($existing.name)"
    }
    else {
        $body = $product | Select-Object Name, Price, Description, Category, ImageUrl, Brand, Sku | ConvertTo-Json -Compress
        $existing = Invoke-RestMethod -Method Put -Uri "$GatewayBaseUrl/catalog/products/$($existing.id)" -Headers $headers -ContentType "application/json" -Body $body -TimeoutSec 15
        Write-Host "[updated] $($existing.sku) - $($existing.name)"
    }

    $stockBody = @{ stockQuantity = $product.StockQuantity } | ConvertTo-Json -Compress
    Invoke-RestMethod -Method Put -Uri "$GatewayBaseUrl/catalog/products/$($existing.id)/stock" -Headers $headers -ContentType "application/json" -Body $stockBody -TimeoutSec 15 | Out-Null
    Write-Host "[inventory synchronized] $($existing.sku)"

    if ($PruneLegacyDuplicates) {
        foreach ($duplicate in $legacyDuplicates) {
            Invoke-RestMethod -Method Delete -Uri "$GatewayBaseUrl/catalog/products/$($duplicate.id)" -Headers $headers -TimeoutSec 15 | Out-Null
            Write-Host "[deactivated legacy duplicate] $($duplicate.id) - $($duplicate.name)"
        }
    }
}

if ($InventoryPropagationWaitSeconds -gt 0) {
    Start-Sleep -Seconds $InventoryPropagationWaitSeconds
}

Write-Host "Portfolio catalog import completed: $($catalog.Count) products."