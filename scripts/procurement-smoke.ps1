[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://api.localhost:5027",
    [string]$EnvFile = ".env.local-prod",
    [ValidateRange(1, 100)]
    [int]$ReceiptQuantity = 2
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

function Require-Value {
    param([string]$Value, [string]$Name)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$Name is required."
    }

    return $Value
}

$adminUserName = Require-Value (Get-EnvFileValue "MICROSHOP_BOOTSTRAP_ADMIN_USERNAME") "MICROSHOP_BOOTSTRAP_ADMIN_USERNAME"
$adminPassword = Require-Value (Get-EnvFileValue "MICROSHOP_BOOTSTRAP_ADMIN_PASSWORD") "MICROSHOP_BOOTSTRAP_ADMIN_PASSWORD"

Write-Host "Running procurement smoke against $GatewayBaseUrl"
$login = Invoke-RestMethod -Uri "$GatewayBaseUrl/auth/login" -Method Post -ContentType "application/json" -Body (@{
    userName = $adminUserName
    password = $adminPassword
} | ConvertTo-Json)
$accessToken = Require-Value $login.accessToken "Administrator access token"
$headers = @{ Authorization = "Bearer $accessToken"; Accept = "application/json" }

$customerUserName = "procurement-rbac-" + [Guid]::NewGuid().ToString("N").Substring(0, 10)
$customerPassword = "ProcurementRbac!2026"
$registration = Invoke-WebRequest -Uri "$GatewayBaseUrl/auth/register" -Method Post -ContentType "application/json" -Body (@{
    userName = $customerUserName
    email = "$customerUserName@example.test"
    password = $customerPassword
} | ConvertTo-Json) -UseBasicParsing
if ($registration.StatusCode -ne 201) {
    throw "Customer registration for RBAC verification failed."
}
$customerLogin = Invoke-RestMethod -Uri "$GatewayBaseUrl/auth/login" -Method Post -ContentType "application/json" -Body (@{
    userName = $customerUserName
    password = $customerPassword
} | ConvertTo-Json)
try {
    $null = Invoke-WebRequest -Uri "$GatewayBaseUrl/suppliers?page=0&pageSize=1" -Headers @{ Authorization = "Bearer $($customerLogin.accessToken)" } -UseBasicParsing
    throw "Customer token unexpectedly accessed supplier operations."
}
catch {
    if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -ne 403) {
        throw "Supplier RBAC returned HTTP $([int]$_.Exception.Response.StatusCode), expected 403."
    }
    if (-not $_.Exception.Response -and $_.Exception.Message -notlike "*unexpectedly accessed*") {
        throw
    }
    if ($_.Exception.Message -like "*unexpectedly accessed*") {
        throw
    }
}
Write-Host "[ok] customer token is denied from supplier operations"

$supplierPage = Invoke-RestMethod -Uri "$GatewayBaseUrl/suppliers?page=0&pageSize=1" -Headers $headers
if ($supplierPage.page -ne 0 -or $supplierPage.pageSize -ne 1 -or $null -eq $supplierPage.items) {
    throw "Supplier pagination contract is invalid."
}

$purchaseOrderPage = Invoke-RestMethod -Uri "$GatewayBaseUrl/procurement/purchase-orders?page=0&pageSize=1" -Headers $headers
if ($purchaseOrderPage.page -ne 0 -or $purchaseOrderPage.pageSize -ne 1 -or $null -eq $purchaseOrderPage.items) {
    throw "Purchase-order pagination contract is invalid."
}
Write-Host "[ok] paginated supplier and purchase-order contracts"

$products = Invoke-RestMethod -Uri "$GatewayBaseUrl/catalog/products" -Headers $headers
$product = $products[0]
if ($null -eq $product -or [string]::IsNullOrWhiteSpace($product.id)) {
    throw "No catalog product is available for the procurement smoke."
}

$inventoryItems = Invoke-RestMethod -Uri "$GatewayBaseUrl/inventory/admin/items" -Headers $headers
$inventoryBefore = @($inventoryItems | Where-Object { $_.productId -eq $product.id })[0]
if ($null -eq $inventoryBefore) {
    throw "Inventory does not contain the selected catalog product."
}

$supplier = Invoke-RestMethod -Uri "$GatewayBaseUrl/suppliers" -Method Post -Headers $headers -ContentType "application/json" -Body (@{
    name = "Procurement smoke $(Get-Random)"
    contactEmail = "procurement-smoke@example.test"
} | ConvertTo-Json)

$purchaseOrder = Invoke-RestMethod -Uri "$GatewayBaseUrl/procurement/purchase-orders" -Method Post -Headers $headers -ContentType "application/json" -Body (@{
    supplierId = $supplier.id
    currency = "USD"
    lines = @(@{
        productId = $product.id
        productName = $product.name
        quantity = $ReceiptQuantity
        unitCost = 10.00
    })
} | ConvertTo-Json -Depth 4)
if ($purchaseOrder.status -ne "DRAFT") {
    throw "Purchase order was not created as DRAFT."
}

$submitted = Invoke-RestMethod -Uri "$GatewayBaseUrl/procurement/purchase-orders/$($purchaseOrder.id)/submit" -Method Post -Headers $headers
if ($submitted.status -ne "SUBMITTED") {
    throw "Purchase order was not submitted."
}

$inventoryAfterSubmit = @((Invoke-RestMethod -Uri "$GatewayBaseUrl/inventory/admin/items" -Headers $headers) | Where-Object { $_.productId -eq $product.id })[0]
if ([int]$inventoryAfterSubmit.stockQuantity -ne [int]$inventoryBefore.stockQuantity) {
    throw "Submitting a purchase order changed sellable inventory before receipt."
}
Write-Host "[ok] purchase-order state does not change inventory"

$receiptUrl = "$GatewayBaseUrl/procurement/purchase-orders/$($purchaseOrder.id)/receive"
$jobs = 1..2 | ForEach-Object {
    Start-Job -ScriptBlock {
        param($url, $token)
        Invoke-RestMethod -Uri $url -Method Post -Headers @{ Authorization = "Bearer $token"; Accept = "application/json" }
    } -ArgumentList $receiptUrl, $accessToken
}

$null = Wait-Job -Job $jobs -Timeout 30
$responses = @($jobs | Receive-Job)
$jobs | Remove-Job -Force
if ($responses.Count -ne 2 -or @($responses | Where-Object { $_.status -ne "RECEIVED" }).Count -ne 0) {
    throw "Concurrent receipt requests did not resolve to RECEIVED."
}
if (@($responses.receiptId | Select-Object -Unique).Count -ne 1) {
    throw "Concurrent receipt requests returned different receipt ids."
}

$inventoryAfterReceipt = @((Invoke-RestMethod -Uri "$GatewayBaseUrl/inventory/admin/items" -Headers $headers) | Where-Object { $_.productId -eq $product.id })[0]
$expectedStock = [int]$inventoryBefore.stockQuantity + $ReceiptQuantity
if ([int]$inventoryAfterReceipt.stockQuantity -ne $expectedStock) {
    throw "Receipt should add stock exactly once. Expected $expectedStock, got $($inventoryAfterReceipt.stockQuantity)."
}
Write-Host "[ok] concurrent receipt is idempotent and Inventory applied stock once"

$auditPage = Invoke-RestMethod -Uri "$GatewayBaseUrl/procurement/audit?purchaseOrderId=$($purchaseOrder.id)&page=0&pageSize=25" -Headers $headers
$requiredAuditActions = @("purchase-order.created", "purchase-order.submitted", "purchase-order.receipt-requested", "purchase-order.received")
$recordedActions = @($auditPage.items | ForEach-Object { $_.action })
if (@($requiredAuditActions | Where-Object { $_ -notin $recordedActions }).Count -ne 0) {
    throw "Procurement audit trail is incomplete."
}
if (@($auditPage.items | Where-Object { [string]::IsNullOrWhiteSpace($_.actor) }).Count -ne 0) {
    throw "Procurement audit entries are missing their actor."
}
Write-Host "[ok] procurement audit trail with actor"

[PSCustomObject]@{
    SupplierId = $supplier.id
    PurchaseOrderId = $purchaseOrder.id
    ReceiptId = $responses[0].receiptId
    ProductId = $product.id
    InventoryBefore = $inventoryBefore.stockQuantity
    InventoryAfterReceipt = $inventoryAfterReceipt.stockQuantity
    ReceiptQuantity = $ReceiptQuantity
} | Format-List

Write-Host "Procurement smoke passed."