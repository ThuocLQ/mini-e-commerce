[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://api.localhost:5027"
)

$ErrorActionPreference = "Stop"
$GatewayBaseUrl = $GatewayBaseUrl.TrimEnd("/")

function Assert-ProblemResponse {
    param(
        [string]$Path,
        [int]$ExpectedStatus,
        [string]$ExpectedType
    )

    try {
        Invoke-WebRequest -Uri "$GatewayBaseUrl$Path" -UseBasicParsing -TimeoutSec 10 | Out-Null
        throw "Expected $Path to return HTTP $ExpectedStatus."
    }
    catch {
        $response = $_.Exception.Response
        if ($null -eq $response) { throw }
        if ([int]$response.StatusCode -ne $ExpectedStatus) {
            throw "$Path returned HTTP $([int]$response.StatusCode), expected $ExpectedStatus."
        }

        $reader = [IO.StreamReader]::new($response.GetResponseStream())
        $body = $reader.ReadToEnd()
        $reader.Dispose()
        $problem = $body | ConvertFrom-Json
        if ($problem.type -ne $ExpectedType) {
            throw "$Path returned unexpected problem type '$($problem.type)'."
        }
        if ([string]::IsNullOrWhiteSpace($response.Headers["X-Correlation-ID"])) {
            throw "$Path did not return X-Correlation-ID."
        }

        Write-Host "[ok] $Path returns protected ProblemDetails"
    }
}

try {
    Invoke-WebRequest -Uri "$GatewayBaseUrl/orders" -UseBasicParsing -TimeoutSec 10 | Out-Null
    throw "Anonymous caller unexpectedly accessed /orders."
}
catch {
    $response = $_.Exception.Response
    if ($null -eq $response -or [int]$response.StatusCode -ne 401) { throw }
    $reader = [IO.StreamReader]::new($response.GetResponseStream())
    $body = $reader.ReadToEnd()
    $reader.Dispose()
    $problem = $body | ConvertFrom-Json
    if ($problem.type -ne "https://microshop.dev/problems/unauthorized") {
        throw "/orders did not return the expected unauthorized ProblemDetails type."
    }
    Write-Host "[ok] /orders requires authentication and returns ProblemDetails"
}

Assert-ProblemResponse -Path "/debug/order-summaries" -ExpectedStatus 404 -ExpectedType "https://microshop.dev/problems/debug-route-not-available"
Assert-ProblemResponse -Path "/orders/00000000-0000-0000-0000-000000000000/payment-result" -ExpectedStatus 404 -ExpectedType "https://microshop.dev/problems/internal-route-not-available"
Assert-ProblemResponse -Path "/__p1-route-does-not-exist" -ExpectedStatus 404 -ExpectedType "https://microshop.dev/problems/http-404"

Write-Host "Gateway route security smoke passed."