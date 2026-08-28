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
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Name
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

    foreach ($property in @("id", "name", "description", "stockQuantity", "category", "imageUrl")) {
        if ([string]$Actual.$property -ne [string]$Expected.$property) {
            throw "$Operation returned a different $property value for product '$($Expected.id)'."
        }
    }

    if ([decimal]$Actual.price -ne [decimal]$Expected.price) {
        throw "$Operation returned a different price for product '$($Expected.id)'."
    }
}

function Get-DiscoveryPage {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$Prefix,
        [Parameter(Mandatory = $true)][string]$Sort,
        [Parameter(Mandatory = $true)][int]$PageSize,
        [string]$Category,
        [string]$Cursor
    )

    $query = @(
        "sort=$([Uri]::EscapeDataString($Sort))",
        "pageSize=$PageSize"
    )

    if (-not [string]::IsNullOrWhiteSpace($Category)) {
        $query += "category=$([Uri]::EscapeDataString($Category))"
    }

    if (-not [string]::IsNullOrWhiteSpace($Cursor)) {
        $query += "cursor=$([Uri]::EscapeDataString($Cursor))"
    }

    return Invoke-AnonymousGet "$BaseUrl$Prefix/products/discovery?$($query -join '&')"
}

function Assert-DiscoveryPageMatches {
    param(
        [Parameter(Mandatory = $true)]$Actual,
        [Parameter(Mandatory = $true)]$Expected,
        [Parameter(Mandatory = $true)][string]$Operation
    )

    if ([int]$Actual.pageSize -ne [int]$Expected.pageSize) {
        throw "$Operation returned a different pageSize."
    }

    if ([string]$Actual.sort -ne [string]$Expected.sort) {
        throw "$Operation returned a different sort."
    }

    if ([string]$Actual.nextCursor -ne [string]$Expected.nextCursor) {
        throw "$Operation returned a different nextCursor."
    }

    $actualItems = @($Actual.items)
    $expectedItems = @($Expected.items)
    if ($actualItems.Count -ne $expectedItems.Count) {
        throw "$Operation returned a different item count."
    }

    for ($index = 0; $index -lt $expectedItems.Count; $index++) {
        Assert-ProductMatches -Actual $actualItems[$index] -Expected $expectedItems[$index] -Operation "$Operation item $index"
    }
}

Assert-AbsoluteOrigin -Value $GatewayBaseUrl -Name "GatewayBaseUrl"
Assert-AbsoluteOrigin -Value $StorefrontBaseUrl -Name "StorefrontBaseUrl"

# The baseline validates list, product lookup, search, and anonymous access before this discovery contract suite.
& (Join-Path $PSScriptRoot "portfolio-catalog-real-data-smoke.ps1") `
    -GatewayBaseUrl $GatewayBaseUrl `
    -StorefrontBaseUrl $StorefrontBaseUrl `
    -TimeoutSeconds $TimeoutSeconds

Write-Host "Running portfolio catalog discovery real-data smoke against $StorefrontBaseUrl and $GatewayBaseUrl"

$sort = "name_asc"
$pageSize = 2
$gatewayFirstResponse = Get-DiscoveryPage -BaseUrl $GatewayBaseUrl -Prefix "/catalog" -Sort $sort -PageSize $pageSize
$storefrontFirstResponse = Get-DiscoveryPage -BaseUrl $StorefrontBaseUrl -Prefix "/api/catalog" -Sort $sort -PageSize $pageSize
Assert-Status -Actual $gatewayFirstResponse.StatusCode -Expected 200 -Operation "Anonymous gateway product discovery"
Assert-Status -Actual $storefrontFirstResponse.StatusCode -Expected 200 -Operation "Anonymous Storefront BFF product discovery"

$gatewayFirst = $gatewayFirstResponse.Content | ConvertFrom-Json
$storefrontFirst = $storefrontFirstResponse.Content | ConvertFrom-Json
Assert-DiscoveryPageMatches -Actual $storefrontFirst -Expected $gatewayFirst -Operation "Storefront BFF product discovery"

$metadataProduct = @($gatewayFirst.items | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_.category) -and
    -not [string]::IsNullOrWhiteSpace($_.imageUrl)
} | Select-Object -First 1)[0]
if ($null -eq $metadataProduct) {
    throw "Product discovery did not return persisted category and imageUrl metadata. Run the current portfolio seed before this verification."
}

$imageUri = [Uri]$metadataProduct.imageUrl
if (-not $imageUri.IsAbsoluteUri -or $imageUri.Scheme -notin @("http", "https")) {
    throw "Product discovery returned a non-HTTP imageUrl for product '$($metadataProduct.id)'."
}

Write-Host "[ok] discovery returns persisted category and imageUrl metadata through Gateway and Storefront BFF"

$gatewayCategoryResponse = Get-DiscoveryPage -BaseUrl $GatewayBaseUrl -Prefix "/catalog" -Sort $sort -PageSize 48 -Category $metadataProduct.category
$storefrontCategoryResponse = Get-DiscoveryPage -BaseUrl $StorefrontBaseUrl -Prefix "/api/catalog" -Sort $sort -PageSize 48 -Category $metadataProduct.category
Assert-Status -Actual $gatewayCategoryResponse.StatusCode -Expected 200 -Operation "Anonymous gateway category discovery"
Assert-Status -Actual $storefrontCategoryResponse.StatusCode -Expected 200 -Operation "Anonymous Storefront BFF category discovery"

$gatewayCategoryPage = $gatewayCategoryResponse.Content | ConvertFrom-Json
$storefrontCategoryPage = $storefrontCategoryResponse.Content | ConvertFrom-Json
Assert-DiscoveryPageMatches -Actual $storefrontCategoryPage -Expected $gatewayCategoryPage -Operation "Storefront BFF category discovery"
$gatewayCategoryItems = @($gatewayCategoryPage.items)
if ($gatewayCategoryItems.Count -eq 0 -or $null -eq @($gatewayCategoryItems | Where-Object { $_.id -eq $metadataProduct.id } | Select-Object -First 1)) {
    throw "Category discovery did not return the selected persisted product '$($metadataProduct.id)'."
}

$unexpectedCategoryProduct = @($gatewayCategoryItems | Where-Object { $_.category -ine $metadataProduct.category } | Select-Object -First 1)[0]
if ($null -ne $unexpectedCategoryProduct) {
    throw "Category discovery returned a product outside category '$($metadataProduct.category)'."
}

Write-Host "[ok] discovery category filter returns only persisted category '$($metadataProduct.category)'"

$firstIds = @($gatewayFirst.items | ForEach-Object { $_.id })
if ([string]::IsNullOrWhiteSpace($gatewayFirst.nextCursor)) {
    throw "Product discovery did not return a nextCursor. Ensure the portfolio seed has more products than the requested page size."
}

$gatewayNextResponse = Get-DiscoveryPage -BaseUrl $GatewayBaseUrl -Prefix "/catalog" -Sort $sort -PageSize $pageSize -Cursor $gatewayFirst.nextCursor
$storefrontNextResponse = Get-DiscoveryPage -BaseUrl $StorefrontBaseUrl -Prefix "/api/catalog" -Sort $sort -PageSize $pageSize -Cursor $storefrontFirst.nextCursor
Assert-Status -Actual $gatewayNextResponse.StatusCode -Expected 200 -Operation "Gateway product discovery cursor advance"
Assert-Status -Actual $storefrontNextResponse.StatusCode -Expected 200 -Operation "Storefront BFF product discovery cursor advance"

$gatewayNext = $gatewayNextResponse.Content | ConvertFrom-Json
$storefrontNext = $storefrontNextResponse.Content | ConvertFrom-Json
Assert-DiscoveryPageMatches -Actual $storefrontNext -Expected $gatewayNext -Operation "Storefront BFF product discovery cursor advance"

$nextIds = @($gatewayNext.items | ForEach-Object { $_.id })
$overlappingProductId = @($nextIds | Where-Object { $_ -in $firstIds } | Select-Object -First 1)[0]
if ($nextIds.Count -eq 0 -or $null -ne $overlappingProductId) {
    throw "Product discovery cursor advance did not return a distinct next page."
}

$gatewayRepeatResponse = Get-DiscoveryPage -BaseUrl $GatewayBaseUrl -Prefix "/catalog" -Sort $sort -PageSize $pageSize -Cursor $gatewayFirst.nextCursor
$storefrontRepeatResponse = Get-DiscoveryPage -BaseUrl $StorefrontBaseUrl -Prefix "/api/catalog" -Sort $sort -PageSize $pageSize -Cursor $storefrontFirst.nextCursor
Assert-Status -Actual $gatewayRepeatResponse.StatusCode -Expected 200 -Operation "Gateway repeated discovery cursor advance"
Assert-Status -Actual $storefrontRepeatResponse.StatusCode -Expected 200 -Operation "Storefront BFF repeated discovery cursor advance"
Assert-DiscoveryPageMatches -Actual ($gatewayRepeatResponse.Content | ConvertFrom-Json) -Expected $gatewayNext -Operation "Gateway repeated discovery cursor advance"
Assert-DiscoveryPageMatches -Actual ($storefrontRepeatResponse.Content | ConvertFrom-Json) -Expected $storefrontNext -Operation "Storefront BFF repeated discovery cursor advance"
Write-Host "[ok] discovery cursor advances to a stable non-overlapping page through Gateway and Storefront BFF"

$invalidCursor = "not-a-valid-cursor"
$gatewayInvalidResponse = Get-DiscoveryPage -BaseUrl $GatewayBaseUrl -Prefix "/catalog" -Sort $sort -PageSize $pageSize -Cursor $invalidCursor
$storefrontInvalidResponse = Get-DiscoveryPage -BaseUrl $StorefrontBaseUrl -Prefix "/api/catalog" -Sort $sort -PageSize $pageSize -Cursor $invalidCursor
Assert-Status -Actual $gatewayInvalidResponse.StatusCode -Expected 400 -Operation "Gateway invalid discovery cursor"
Assert-Status -Actual $storefrontInvalidResponse.StatusCode -Expected 400 -Operation "Storefront BFF invalid discovery cursor"
Write-Host "[ok] invalid discovery cursor returns 400 through Gateway and Storefront BFF"

[PSCustomObject]@{
    Category = $metadataProduct.category
    ProductId = $metadataProduct.id
    ProductName = $metadataProduct.name
    ImageUrl = $metadataProduct.imageUrl
    CursorAdvanced = $true
    AnonymousDiscoveryAccess = $true
    RuntimeDataPathsMatch = $true
} | Format-List

Write-Host "Portfolio catalog discovery real-data smoke passed."
