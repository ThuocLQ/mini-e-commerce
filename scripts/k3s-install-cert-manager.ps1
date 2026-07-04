[CmdletBinding()]
param(
    [string]$Email,
    [string]$ClusterIssuerName = "letsencrypt-prod",
    [string]$CertManagerVersion = "v1.15.3",
    [switch]$UseStaging
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Email)) {
    throw "Email is required for Let's Encrypt account registration. Pass -Email you@example.com."
}

$issuerServer = if ($UseStaging) {
    "https://acme-staging-v02.api.letsencrypt.org/directory"
}
else {
    "https://acme-v02.api.letsencrypt.org/directory"
}

$manifestUrl = "https://github.com/cert-manager/cert-manager/releases/download/$CertManagerVersion/cert-manager.yaml"

Write-Host "Installing cert-manager $CertManagerVersion..."
kubectl apply -f $manifestUrl
if ($LASTEXITCODE -ne 0) {
    throw "Failed to apply cert-manager manifests."
}

Write-Host "Waiting for cert-manager deployments..."
kubectl rollout status deployment/cert-manager -n cert-manager --timeout=180s
if ($LASTEXITCODE -ne 0) {
    throw "cert-manager deployment did not become ready."
}

kubectl rollout status deployment/cert-manager-webhook -n cert-manager --timeout=180s
if ($LASTEXITCODE -ne 0) {
    throw "cert-manager-webhook deployment did not become ready."
}

kubectl rollout status deployment/cert-manager-cainjector -n cert-manager --timeout=180s
if ($LASTEXITCODE -ne 0) {
    throw "cert-manager-cainjector deployment did not become ready."
}

$issuerYaml = @"
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: $ClusterIssuerName
spec:
  acme:
    email: $Email
    server: $issuerServer
    privateKeySecretRef:
      name: $ClusterIssuerName-account-key
    solvers:
      - http01:
          ingress:
            class: traefik
"@

Write-Host "Applying ClusterIssuer '$ClusterIssuerName'..."
$issuerYaml | kubectl apply -f -
if ($LASTEXITCODE -ne 0) {
    throw "Failed to apply ClusterIssuer '$ClusterIssuerName'."
}

Write-Host "cert-manager is ready. ClusterIssuer '$ClusterIssuerName' points to $issuerServer."
