[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://api.localhost:5027",
    [string]$StorefrontBaseUrl = "http://localhost:5027",
    [string]$EnvFile = ".env.local-prod",
    [switch]$SkipBrowserE2E,
    [switch]$SkipObservability
)

$ErrorActionPreference = "Stop"

function Invoke-ReleaseCheck {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Host "`n==> $Name"
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $EnvFile)) {
    throw "Missing $EnvFile. Configure local secrets before verifying a release."
}

Invoke-ReleaseCheck -Name "Compose configuration" -Action {
    docker compose --env-file $EnvFile -f compose.local-prod.yml -f compose.portfolio.yml --profile read-model config --quiet
}

Invoke-ReleaseCheck -Name "Gateway route security" -Action {
    & (Join-Path $PSScriptRoot "gateway-route-security-smoke.ps1") -GatewayBaseUrl $GatewayBaseUrl
}

Invoke-ReleaseCheck -Name "Local production smoke" -Action {
    & (Join-Path $PSScriptRoot "local-prod-smoke.ps1") -GatewayBaseUrl $GatewayBaseUrl -EnvFile $EnvFile -VerifyPortfolioFrontends
}

Invoke-ReleaseCheck -Name "Customer cancellation journey" -Action {
    & (Join-Path $PSScriptRoot "portfolio-customer-smoke.ps1") -StorefrontBaseUrl $StorefrontBaseUrl -GatewayBaseUrl $GatewayBaseUrl -EnvFile $EnvFile -Scenario Cancellation
}

Invoke-ReleaseCheck -Name "Customer fulfillment journey" -Action {
    & (Join-Path $PSScriptRoot "portfolio-customer-smoke.ps1") -StorefrontBaseUrl $StorefrontBaseUrl -GatewayBaseUrl $GatewayBaseUrl -EnvFile $EnvFile -Scenario Fulfillment
}

Invoke-ReleaseCheck -Name "Procurement RBAC and idempotency" -Action {
    & (Join-Path $PSScriptRoot "procurement-smoke.ps1") -GatewayBaseUrl $GatewayBaseUrl -EnvFile $EnvFile
}

Invoke-ReleaseCheck -Name "Session revocation" -Action {
    & (Join-Path $PSScriptRoot "session-revocation-smoke.ps1") -GatewayBaseUrl $GatewayBaseUrl -StorefrontBaseUrl $StorefrontBaseUrl
}

if (-not $SkipObservability) {
    Invoke-ReleaseCheck -Name "Observability targets" -Action {
        & (Join-Path $PSScriptRoot "local-prod-observability-smoke.ps1") -SkipGatewaySmoke
    }
}

if (-not $SkipBrowserE2E) {
    $corepack = Get-Command corepack.cmd -ErrorAction Stop
    Invoke-ReleaseCheck -Name "Storefront browser E2E baseline" -Action {
        & $corepack.Source pnpm --dir (Join-Path $PSScriptRoot "..\Frontend\e2e") test
    }
}

Write-Host "`nPortfolio release verification passed. The next allowed operation is portfolio-public-up.ps1, which creates public Cloudflare URLs."
