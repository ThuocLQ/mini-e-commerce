[CmdletBinding()]
param(
    [string]$StorefrontBaseUrl = "http://localhost:5027",
    [string]$UserName,
    [string]$Password = "PortfolioSmoke!2026"
)

$ErrorActionPreference = "Stop"
$StorefrontBaseUrl = $StorefrontBaseUrl.TrimEnd("/")
$storefrontUri = [Uri]$StorefrontBaseUrl
if (-not $storefrontUri.IsAbsoluteUri -or $storefrontUri.GetLeftPart([UriPartial]::Authority) -ne $StorefrontBaseUrl) {
    throw "StorefrontBaseUrl must be an absolute origin without a path, query, or fragment."
}

if ([string]::IsNullOrWhiteSpace($UserName)) {
    $UserName = "portfolio-smoke-" + [Guid]::NewGuid().ToString("N").Substring(0, 10)
}

$headers = @{ Origin = $StorefrontBaseUrl; Accept = "application/json" }
$credentials = @{ userName = $UserName; password = $Password } | ConvertTo-Json

function Assert-Status {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Actual,

        [Parameter(Mandatory = $true)]
        [int]$Expected,

        [Parameter(Mandatory = $true)]
        [string]$Operation
    )

    if ($Actual -ne $Expected) {
        throw "$Operation returned HTTP $Actual; expected HTTP $Expected."
    }
}

Write-Host "Running Storefront customer journey smoke against $StorefrontBaseUrl"

$registration = Invoke-WebRequest -Uri "$StorefrontBaseUrl/api/session" -Method Put -Headers $headers -ContentType "application/json" -Body $credentials -UseBasicParsing
Assert-Status -Actual $registration.StatusCode -Expected 201 -Operation "Customer registration"
Write-Host "[ok] customer registration"

$login = Invoke-WebRequest -Uri "$StorefrontBaseUrl/api/session" -Method Post -Headers $headers -ContentType "application/json" -Body $credentials -SessionVariable storefrontSession -UseBasicParsing
Assert-Status -Actual $login.StatusCode -Expected 200 -Operation "Customer sign-in"
$customer = $login.Content | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($customer.user.userId)) {
    throw "Customer sign-in did not return a user id."
}
Write-Host "[ok] customer sign-in"

$addressInput = @{ label = "Portfolio smoke"; recipientName = "Portfolio Smoke"; line1 = "100 Snapshot Lane"; line2 = $null; city = "Bangkok"; countryCode = "TH"; postalCode = "10110"; makeDefault = $false }
$addressHeaders = @{ Origin = $StorefrontBaseUrl; Accept = "application/json"; "Idempotency-Key" = [Guid]::NewGuid().ToString() }
$address = Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/addresses" -Method Post -Headers $addressHeaders -ContentType "application/json" -Body ($addressInput | ConvertTo-Json) -WebSession $storefrontSession
if ([string]::IsNullOrWhiteSpace($address.id)) {
    throw "Customer address creation did not return an id."
}

$defaultAddress = Invoke-WebRequest -Uri "$StorefrontBaseUrl/api/addresses/$($address.id)/default" -Method Put -Headers $headers -WebSession $storefrontSession -UseBasicParsing
Assert-Status -Actual $defaultAddress.StatusCode -Expected 204 -Operation "Customer address defaulting"
$addresses = Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/addresses" -Method Get -WebSession $storefrontSession
if ($null -eq @($addresses | Where-Object { $_.id -eq $address.id -and $_.isDefault -and -not $_.isArchived })[0]) {
    throw "Created address was not returned as the active default address."
}
Write-Host "[ok] customer address creation and defaulting"

$products = Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/catalog/products" -Method Get
$product = @($products | Where-Object { $_.stockQuantity -gt 0 -and $_.price -gt 0 } | Select-Object -First 1)[0]
if ($null -eq $product) {
    throw "No positive-priced in-stock product was available for customer checkout."
}
Write-Host "[ok] catalog product $($product.name)"

$cart = Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/cart/$($customer.user.userId)/items" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ productId = $product.id; quantity = 1 } | ConvertTo-Json) -WebSession $storefrontSession
if ([string]::IsNullOrWhiteSpace($cart.basketId) -or @($cart.items).Count -eq 0) {
    throw "Add-to-cart did not return a populated basket."
}
Write-Host "[ok] add item to cart"

$order = Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/checkout" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ basketId = $cart.basketId; basketVersion = $cart.version; shippingAddressId = $address.id; idempotencyKey = [Guid]::NewGuid().ToString() } | ConvertTo-Json) -WebSession $storefrontSession
if ([string]::IsNullOrWhiteSpace($order.id) -or $order.status -ne "PendingPayment") {
    throw "Checkout did not create a PendingPayment order."
}
Write-Host "[ok] checkout with shipping address"

$orders = Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/orders" -Method Get -WebSession $storefrontSession
$persistedOrder = @($orders | Where-Object { $_.id -eq $order.id })[0]
if ($null -eq $persistedOrder) {
    throw "Created order was not visible in the customer order list."
}
if ($null -eq $persistedOrder.shippingAddress -or $persistedOrder.shippingAddress.addressId -ne $address.id -or $persistedOrder.shippingAddress.line1 -ne $addressInput.line1 -or $persistedOrder.shippingAddress.recipientName -ne $addressInput.recipientName) {
    throw "Created order did not contain the expected shipping address snapshot."
}

$changedAddressInput = @{ label = "Portfolio smoke changed"; recipientName = "Portfolio Smoke Changed"; line1 = "200 Changed Lane"; line2 = $null; city = "Bangkok"; countryCode = "TH"; postalCode = "10110"; makeDefault = $true }
$updatedAddress = Invoke-WebRequest -Uri "$StorefrontBaseUrl/api/addresses/$($address.id)" -Method Patch -Headers $headers -ContentType "application/json" -Body ($changedAddressInput | ConvertTo-Json) -WebSession $storefrontSession -UseBasicParsing
Assert-Status -Actual $updatedAddress.StatusCode -Expected 200 -Operation "Customer address update"
$ordersAfterAddressUpdate = Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/orders" -Method Get -WebSession $storefrontSession
$snapshotOrder = @($ordersAfterAddressUpdate | Where-Object { $_.id -eq $order.id })[0]
if ($null -eq $snapshotOrder -or $null -eq $snapshotOrder.shippingAddress -or $snapshotOrder.shippingAddress.addressId -ne $address.id -or $snapshotOrder.shippingAddress.line1 -ne $addressInput.line1 -or $snapshotOrder.shippingAddress.recipientName -ne $addressInput.recipientName -or $snapshotOrder.shippingAddress.line1 -eq $changedAddressInput.line1) {
    throw "Order address snapshot changed after the customer address was updated."
}
Write-Host "[ok] customer order history and immutable address snapshot"

$payment = Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/payments" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ orderId = $order.id } | ConvertTo-Json) -WebSession $storefrontSession
if ([string]::IsNullOrWhiteSpace($payment.id) -or $payment.status -ne "PendingAuthorization") {
    throw "Payment initiation did not return a PendingAuthorization payment."
}
Write-Host "[ok] payment initiation"

[PSCustomObject]@{
    Customer = $customer.user.userName
    Product = $product.name
    OrderId = $order.id
    OrderStatus = $order.status
    PaymentId = $payment.id
    PaymentStatus = $payment.status
    ShippingAddressId = $address.id
    ShippingAddressSnapshotImmutable = $true
} | Format-List

Write-Host "Storefront customer journey smoke passed."