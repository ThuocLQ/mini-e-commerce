[CmdletBinding()]
param(
    [string]$ChartPath = "deploy/k3s/microshop",
    [string]$Namespace = "microshop",
    [string]$ImageTag = "ci",
    [switch]$SkipHelm
)

$ErrorActionPreference = "Stop"

$scripts = @(
    "scripts/k3s-backup.ps1",
    "scripts/k3s-create-secrets.ps1",
    "scripts/k3s-create-ghcr-pull-secret.ps1",
    "scripts/k3s-deploy.ps1",
    "scripts/k3s-install-cert-manager.ps1",
    "scripts/k3s-observability-smoke.ps1",
    "scripts/k3s-restore.ps1",
    "scripts/k3s-smoke.ps1",
    "scripts/k3s-validate.ps1",
    "scripts/local-prod-backup.ps1",
    "scripts/local-prod-restore.ps1",
    "scripts/local-prod-observability-smoke.ps1",
    "scripts/local-prod-observability-up.ps1",
    "scripts/local-prod-rc-verify.ps1",
    "scripts/local-prod-smoke.ps1",
    "scripts/local-prod-up.ps1",
    "scripts/test-kafka-lesson25.ps1"
)

foreach ($script in $scripts) {
    $errors = $null
    [System.Management.Automation.PSParser]::Tokenize((Get-Content $script -Raw), [ref]$errors) | Out-Null

    if ($errors) {
        $errors | Format-List
        throw "PowerShell parser errors found in $script."
    }
}

Write-Host "[ok] PowerShell scripts parsed successfully."

docker compose --env-file .env.example -f compose.local-prod.yml config --quiet
if ($LASTEXITCODE -ne 0) {
    throw "local-prod compose validation failed."
}

docker compose --env-file .env.example -f compose.local-prod.yml -f compose.observability.yml config --quiet
if ($LASTEXITCODE -ne 0) {
    throw "local-prod observability compose validation failed."
}

Write-Host "[ok] Docker Compose files are valid."

if ($SkipHelm) {
    Write-Host "[skip] Helm validation skipped."
    return
}

$helm = Get-Command helm -ErrorAction SilentlyContinue
if ($null -eq $helm) {
    throw "Helm CLI was not found. Install Helm or pass -SkipHelm for local static validation without chart rendering."
}

helm lint $ChartPath --namespace $Namespace
if ($LASTEXITCODE -ne 0) {
    throw "helm lint failed."
}

helm template microshop $ChartPath --namespace $Namespace --set "global.imageTag=$ImageTag" | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "helm template failed."
}

Write-Host "[ok] Helm chart is valid."
