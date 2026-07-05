[CmdletBinding()]
param(
    [string]$Namespace = "microshop",
    [string]$SecretName = "ghcr-pull-secret",
    [string]$RegistryServer = "ghcr.io",
    [Parameter(Mandatory = $true)]
    [string]$UserName,
    [Parameter(Mandatory = $true)]
    [string]$Token,
    [string]$Email = "microshop@example.com"
)

$ErrorActionPreference = "Stop"

kubectl create namespace $Namespace --dry-run=client -o yaml | kubectl apply -f -
if ($LASTEXITCODE -ne 0) {
    throw "Failed to create or update namespace '$Namespace'."
}

kubectl create secret docker-registry $SecretName `
    --namespace $Namespace `
    --docker-server $RegistryServer `
    --docker-username $UserName `
    --docker-password $Token `
    --docker-email $Email `
    --dry-run=client `
    -o yaml | kubectl apply -f -

if ($LASTEXITCODE -ne 0) {
    throw "Failed to create or update image pull secret '$SecretName'."
}

Write-Host "Image pull secret '$SecretName' is ready in namespace '$Namespace'."
