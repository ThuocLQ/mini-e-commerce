[CmdletBinding()]
param(
    [string]$EnvFile = ".env.local-prod",
    [string]$ComposeFile = "compose.local-prod.yml",
    [string]$GatewayBaseUrl = "http://localhost:5027",
    [switch]$Build,
    [switch]$SkipSmoke
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $EnvFile)) {
    throw "Missing $EnvFile. Copy .env.example to $EnvFile and replace every CHANGEME value first."
}

if (-not (Test-Path -Path $ComposeFile)) {
    throw "Missing $ComposeFile."
}

$composeArgs = @("--env-file", $EnvFile, "-f", $ComposeFile, "up", "-d")

if ($Build) {
    $composeArgs += "--build"
}

Write-Host "Starting MicroShop local-prod stack..."
& docker compose @composeArgs

if ($LASTEXITCODE -ne 0) {
    throw "docker compose up failed with exit code $LASTEXITCODE."
}

& docker compose --env-file $EnvFile -f $ComposeFile ps

if ($LASTEXITCODE -ne 0) {
    throw "docker compose ps failed with exit code $LASTEXITCODE."
}

if (-not $SkipSmoke) {
    & (Join-Path $PSScriptRoot "local-prod-smoke.ps1") -GatewayBaseUrl $GatewayBaseUrl

    if ($LASTEXITCODE -ne 0) {
        throw "local-prod smoke failed with exit code $LASTEXITCODE."
    }
}
