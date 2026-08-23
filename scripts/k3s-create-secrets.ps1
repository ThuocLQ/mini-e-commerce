[CmdletBinding()]
param(
    [string]$Namespace = "microshop",
    [string]$SecretName = "microshop-secrets",
    [string]$EnvFile = ".env.k3s"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $EnvFile)) {
    throw "Missing $EnvFile. Copy .env.k3s.example to $EnvFile and replace every CHANGEME value first."
}

$requiredKeys = @(
    "MICROSHOP_POSTGRES_PASSWORD",
    "MICROSHOP_RABBITMQ_PASSWORD",
    "MICROSHOP_MONGO_PASSWORD",
    "MICROSHOP_JWT_SECRET_KEY",
    "MICROSHOP_PAYMENT_WEBHOOK_SECRET",
    "MICROSHOP_INTERNAL_API_KEY"
)

$values = @{}

foreach ($line in Get-Content -Path $EnvFile) {
    $trimmed = $line.Trim()

    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
        continue
    }

    $separatorIndex = $trimmed.IndexOf("=")
    if ($separatorIndex -lt 1) {
        continue
    }

    $key = $trimmed.Substring(0, $separatorIndex).Trim()
    $value = $trimmed.Substring($separatorIndex + 1).Trim()
    $values[$key] = $value
}

foreach ($key in $requiredKeys) {
    if (-not $values.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($values[$key])) {
        throw "Missing required key '$key' in $EnvFile."
    }

    if ($values[$key].Contains("CHANGEME", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Key '$key' still contains CHANGEME in $EnvFile."
    }
}

kubectl create namespace $Namespace --dry-run=client -o yaml | kubectl apply -f -
if ($LASTEXITCODE -ne 0) {
    throw "Failed to create or update namespace '$Namespace'."
}

kubectl create secret generic $SecretName `
    --namespace $Namespace `
    --from-literal=postgres-password=$($values["MICROSHOP_POSTGRES_PASSWORD"]) `
    --from-literal=rabbitmq-password=$($values["MICROSHOP_RABBITMQ_PASSWORD"]) `
    --from-literal=mongo-password=$($values["MICROSHOP_MONGO_PASSWORD"]) `
    --from-literal=jwt-secret-key=$($values["MICROSHOP_JWT_SECRET_KEY"]) `
    --from-literal=payment-webhook-secret=$($values["MICROSHOP_PAYMENT_WEBHOOK_SECRET"]) `
    --from-literal=internal-api-key=$($values["MICROSHOP_INTERNAL_API_KEY"]) `
    --dry-run=client `
    -o yaml | kubectl apply -f -

if ($LASTEXITCODE -ne 0) {
    throw "Failed to create or update secret '$SecretName'."
}

Write-Host "K3s secret '$SecretName' is ready in namespace '$Namespace'."
