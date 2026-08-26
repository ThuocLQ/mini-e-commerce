[CmdletBinding()]
param(
    [string]$StorefrontBaseUrl = "http://localhost:5027",
    [string]$UserName,
    [string]$Password = "PortfolioSmoke!2026"
)

$ErrorActionPreference = "Stop"
$StorefrontBaseUrl = $StorefrontBaseUrl.TrimEnd("/")

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

$products = Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/catalog/products" -Method Get
$product = @($products | Where-Object { $_.stockQuantity -gt 0 } | Select-Object -First 1)[0]
if ($null -eq $product) {
    throw "No in-stock product was available for customer checkout."
}
Write-Host "[ok] catalog product $($product.name)"

$cart = Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/cart/$($customer.user.userId)/items" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ productId = $product.id; quantity = 1 } | ConvertTo-Json) -WebSession $storefrontSession
if ([string]::IsNullOrWhiteSpace($cart.basketId) -or @($cart.items).Count -eq 0) {
    throw "Add-to-cart did not return a populated basket."
}
Write-Host "[ok] add item to cart"

$order = Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/checkout" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ basketId = $cart.basketId; basketVersion = $cart.version; idempotencyKey = [Guid]::NewGuid().ToString() } | ConvertTo-Json) -WebSession $storefrontSession
if ([string]::IsNullOrWhiteSpace($order.id) -or $order.status -ne "PendingPayment") {
    throw "Checkout did not create a PendingPayment order."
}
Write-Host "[ok] checkout"

$orders = Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/orders" -Method Get -WebSession $storefrontSession
if (-not @($orders | Where-Object { $_.id -eq $order.id })) {
    throw "Created order was not visible in the customer order list."
}
Write-Host "[ok] customer order history"

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
} | Format-List

Write-Host "Storefront customer journey smoke passed."