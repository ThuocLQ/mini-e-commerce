[CmdletBinding()]
param(
    [string]$EnvFile = ".env.local-prod",
    [string]$GatewayBaseUrl = "http://localhost:5027",
    [switch]$Build,
    [switch]$SkipSmoke
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $EnvFile)) {
    throw "Missing $EnvFile. Copy .env.example to $EnvFile and replace every CHANGEME value first."
}

$composeArgs = @(
    "--env-file", $EnvFile,
    "-f", "compose.local-prod.yml",
    "-f", "compose.observability.yml",
    "up",
    "-d"
)

if ($Build) {
    $composeArgs += "--build"
}

Write-Host "Starting MicroShop local-prod stack with observability..."
& docker compose @composeArgs

if ($LASTEXITCODE -ne 0) {
    throw "docker compose up failed with exit code $LASTEXITCODE."
}

& docker compose --env-file $EnvFile -f compose.local-prod.yml -f compose.observability.yml ps

if ($LASTEXITCODE -ne 0) {
    throw "docker compose ps failed with exit code $LASTEXITCODE."
}

if (-not $SkipSmoke) {
    & (Join-Path $PSScriptRoot "local-prod-observability-smoke.ps1") -GatewayBaseUrl $GatewayBaseUrl

    if ($LASTEXITCODE -ne 0) {
        throw "local-prod observability smoke failed with exit code $LASTEXITCODE."
    }
}
