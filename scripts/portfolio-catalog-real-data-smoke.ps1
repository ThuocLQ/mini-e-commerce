[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://api.localhost:5027",
    [string]$StorefrontBaseUrl = "http://localhost:5027",
    [int]$TimeoutSeconds = 15
)

$ErrorActionPreference = "Stop"

$GatewayBaseUrl = $GatewayBaseUrl.TrimEnd("/")
$StorefrontBaseUrl = $StorefrontBaseUrl.TrimEnd("/")

function Assert-AbsoluteOrigin {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $uri = [Uri]$Value
    if (-not $uri.IsAbsoluteUri -or $uri.GetLeftPart([UriPartial]::Authority) -ne $Value) {
        throw "$Name must be an absolute origin without a path, query, or fragment."
    }
}

function Invoke-AnonymousGet {
    param([Parameter(Mandatory = $true)][string]$Url)

    try {
        $response = Invoke-WebRequest `
            -Uri $Url `
            -Method Get `
            -Headers @{ Accept = "application/json" } `
            -UseBasicParsing `
            -TimeoutSec $TimeoutSeconds

        return [PSCustomObject]@{
            StatusCode = [int]$response.StatusCode
            Content = $response.Content
        }
    }
    catch {
        if ($null -ne $_.Exception.Response) {
            return [PSCustomObject]@{
                StatusCode = [int]$_.Exception.Response.StatusCode
                Content = $_.ErrorDetails.Message
            }
        }

        throw
    }
}

function Assert-Status {
    param(
        [Parameter(Mandatory = $true)][int]$Actual,
        [Parameter(Mandatory = $true)][int]$Expected,
        [Parameter(Mandatory = $true)][string]$Operation
    )

    if ($Actual -ne $Expected) {
        throw "$Operation returned HTTP $Actual; expected HTTP $Expected."
    }
}

function Assert-ProductMatches {
    param(
        [Parameter(Mandatory = $true)]$Actual,
        [Parameter(Mandatory = $true)]$Expected,
        [Parameter(Mandatory = $true)][string]$Operation
    )

    foreach ($property in @("id", "name", "description", "stockQuantity")) {
        if ([string]$Actual.$property -ne [string]$Expected.$property) {
            throw "$Operation returned a different $property value for product '$($Expected.id)'."
        }
    }

    if ([decimal]$Actual.price -ne [decimal]$Expected.price) {
        throw "$Operation returned a different price for product '$($Expected.id)'."
    }
}

function Get-ProductById {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$Prefix,
        [Parameter(Mandatory = $true)][string]$ProductId
    )

    return Invoke-AnonymousGet "$BaseUrl$Prefix/products/$([Uri]::EscapeDataString($ProductId))"
}

Assert-AbsoluteOrigin -Value $GatewayBaseUrl -Name "GatewayBaseUrl"
Assert-AbsoluteOrigin -Value $StorefrontBaseUrl -Name "StorefrontBaseUrl"

Write-Host "Running portfolio catalog real-data smoke against $StorefrontBaseUrl and $GatewayBaseUrl"

# These calls intentionally have neither cookies nor an Authorization header.
$gatewayListResponse = Invoke-AnonymousGet "$GatewayBaseUrl/catalog/products"
$storefrontListResponse = Invoke-AnonymousGet "$StorefrontBaseUrl/api/catalog/products"
Assert-Status -Actual $gatewayListResponse.StatusCode -Expected 200 -Operation "Anonymous gateway catalog list"
Assert-Status -Actual $storefrontListResponse.StatusCode -Expected 200 -Operation "Anonymous Storefront BFF catalog list"

$gatewayProducts = @($gatewayListResponse.Content | ConvertFrom-Json | Write-Output)
$storefrontProducts = @($storefrontListResponse.Content | ConvertFrom-Json | Write-Output)

if ($gatewayProducts.Count -eq 0) {
    throw "Gateway catalog returned no products. Run the portfolio seed before this verification."
}

if ($gatewayProducts.Count -ne $storefrontProducts.Count) {
    throw "Storefront BFF catalog count ($($storefrontProducts.Count)) did not match the gateway catalog count ($($gatewayProducts.Count))."
}

$selectedProduct = @($gatewayProducts | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_.id) -and
    -not [string]::IsNullOrWhiteSpace($_.name)
} | Sort-Object id | Select-Object -First 1)[0]

if ($null -eq $selectedProduct) {
    throw "Gateway catalog did not return a valid product record."
}

$bffListProduct = @($storefrontProducts | Where-Object { $_.id -eq $selectedProduct.id } | Select-Object -First 1)[0]
if ($null -eq $bffListProduct) {
    throw "Storefront BFF catalog list did not contain the live gateway product '$($selectedProduct.id)'."
}

Assert-ProductMatches -Actual $bffListProduct -Expected $selectedProduct -Operation "Storefront BFF catalog list"
Write-Host "[ok] anonymous catalog list is available through Gateway and Storefront BFF"

$gatewayProductResponse = Get-ProductById -BaseUrl $GatewayBaseUrl -Prefix "/catalog" -ProductId $selectedProduct.id
$storefrontProductResponse = Get-ProductById -BaseUrl $StorefrontBaseUrl -Prefix "/api/catalog" -ProductId $selectedProduct.id
Assert-Status -Actual $gatewayProductResponse.StatusCode -Expected 200 -Operation "Anonymous gateway product lookup"
Assert-Status -Actual $storefrontProductResponse.StatusCode -Expected 200 -Operation "Anonymous Storefront BFF product lookup"

$gatewayProduct = $gatewayProductResponse.Content | ConvertFrom-Json
$storefrontProduct = $storefrontProductResponse.Content | ConvertFrom-Json
Assert-ProductMatches -Actual $gatewayProduct -Expected $selectedProduct -Operation "Gateway product lookup"
Assert-ProductMatches -Actual $storefrontProduct -Expected $selectedProduct -Operation "Storefront BFF product lookup"
Write-Host "[ok] product lookup resolves the same persisted product through both paths"

$searchKeyword = [Uri]::EscapeDataString($selectedProduct.name)
$gatewaySearchResponse = Invoke-AnonymousGet "$GatewayBaseUrl/catalog/products/search?keyword=$searchKeyword"
$storefrontSearchResponse = Invoke-AnonymousGet "$StorefrontBaseUrl/api/catalog/products/search?keyword=$searchKeyword"
Assert-Status -Actual $gatewaySearchResponse.StatusCode -Expected 200 -Operation "Anonymous gateway product search"
Assert-Status -Actual $storefrontSearchResponse.StatusCode -Expected 200 -Operation "Anonymous Storefront BFF product search"

$gatewaySearchProducts = @($gatewaySearchResponse.Content | ConvertFrom-Json | Write-Output)
$storefrontSearchProducts = @($storefrontSearchResponse.Content | ConvertFrom-Json | Write-Output)
if ($null -eq @($gatewaySearchProducts | Where-Object { $_.id -eq $selectedProduct.id } | Select-Object -First 1)) {
    throw "Gateway product search did not return the selected live product '$($selectedProduct.id)'."
}

$bffSearchProduct = @($storefrontSearchProducts | Where-Object { $_.id -eq $selectedProduct.id } | Select-Object -First 1)[0]
if ($null -eq $bffSearchProduct) {
    throw "Storefront BFF product search did not return the selected live product '$($selectedProduct.id)'."
}

Assert-ProductMatches -Actual $bffSearchProduct -Expected $selectedProduct -Operation "Storefront BFF product search"
Write-Host "[ok] product search returns the same live product through both paths"

$missingProductId = [Guid]::NewGuid().ToString("D")
$gatewayMissingResponse = Get-ProductById -BaseUrl $GatewayBaseUrl -Prefix "/catalog" -ProductId $missingProductId
$storefrontMissingResponse = Get-ProductById -BaseUrl $StorefrontBaseUrl -Prefix "/api/catalog" -ProductId $missingProductId
Assert-Status -Actual $gatewayMissingResponse.StatusCode -Expected 404 -Operation "Gateway missing product lookup"
Assert-Status -Actual $storefrontMissingResponse.StatusCode -Expected 404 -Operation "Storefront BFF missing product lookup"
Write-Host "[ok] missing product lookup returns 404 through Gateway and Storefront BFF"

[PSCustomObject]@{
    ProductId = $selectedProduct.id
    ProductName = $selectedProduct.name
    GatewayProductCount = $gatewayProducts.Count
    StorefrontBffProductCount = $storefrontProducts.Count
    AnonymousCatalogAccess = $true
    RuntimeDataPathsMatch = $true
} | Format-List

Write-Host "Portfolio catalog real-data smoke passed."
