[CmdletBinding()]
param(
    [ValidateSet("Core", "Full")]
    [string]$Mode = "Core",
    [string]$EnvFile = ".env.local-prod",
    [string]$GatewayBaseUrl = "http://api.localhost:5027",
    [switch]$Build,
    [switch]$SkipSmoke,
    [switch]$SkipSeed
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $EnvFile)) {
    throw "Missing $EnvFile. Copy .env.example to $EnvFile and replace every CHANGEME value first."
}

$composeArgs = @(
    "--env-file", $EnvFile,
    "-f", "compose.local-prod.yml",
    "-f", "compose.portfolio.yml"
)

if ($Mode -eq "Full") {
    $composeArgs += @("--profile", "read-model")
}

$upArgs = @("up", "-d")
if ($Build) {
    $upArgs += "--build"
}

Write-Host "Starting MicroShop portfolio $Mode profile..."
& docker compose @composeArgs @upArgs
if ($LASTEXITCODE -ne 0) {
    throw "docker compose up failed with exit code $LASTEXITCODE."
}

& docker compose @composeArgs ps
if ($LASTEXITCODE -ne 0) {
    throw "docker compose ps failed with exit code $LASTEXITCODE."
}

if (-not $SkipSeed) {
    & (Join-Path $PSScriptRoot "portfolio-seed.ps1") -GatewayBaseUrl $GatewayBaseUrl -EnvFile $EnvFile
    if ($LASTEXITCODE -ne 0) {
        throw "portfolio seed failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipSmoke) {
    $smokeScript = Join-Path $PSScriptRoot "local-prod-smoke.ps1"

    if ($Mode -eq "Core") {
        & $smokeScript -GatewayBaseUrl $GatewayBaseUrl -EnvFile $EnvFile -VerifyPortfolioFrontends -SkipReadModel
    }
    else {
        & $smokeScript -GatewayBaseUrl $GatewayBaseUrl -EnvFile $EnvFile -VerifyPortfolioFrontends
    }

    if ($LASTEXITCODE -ne 0) {
        throw "portfolio smoke failed with exit code $LASTEXITCODE."
    }
}