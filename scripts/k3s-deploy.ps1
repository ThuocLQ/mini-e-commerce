[CmdletBinding()]
param(
    [string]$Namespace = "microshop",
    [string]$ReleaseName = "microshop",
    [string]$ChartPath = "deploy/k3s/microshop",
    [string]$Domain = "api.example.com",
    [string]$GatewayBaseUrl,
    [string]$ImageTag = "main",
    [string]$ImageRegistry = "ghcr.io/thuoclq",
    [string]$Timeout = "15m",
    [string]$ImagePullSecretName,
    [switch]$SkipSmoke,
    [switch]$SkipObservabilitySmoke
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($GatewayBaseUrl)) {
    $GatewayBaseUrl = "https://$Domain"
}

function Invoke-RequiredCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandName,

        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $CommandName @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$CommandName $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-CurrentHelmRevision {
    param(
        [string]$Name,
        [string]$ReleaseNamespace
    )

    $historyJson = helm history $Name --namespace $ReleaseNamespace -o json 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($historyJson)) {
        return $null
    }

    $history = $historyJson | ConvertFrom-Json
    if ($null -eq $history -or $history.Count -eq 0) {
        return $null
    }

    return ($history | Select-Object -Last 1).revision
}

if (-not (Test-Path -Path $ChartPath)) {
    throw "Chart path '$ChartPath' was not found."
}

$previousRevision = Get-CurrentHelmRevision -Name $ReleaseName -ReleaseNamespace $Namespace

try {
    $namespaceYaml = & kubectl create namespace $Namespace --dry-run=client -o yaml
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to render namespace '$Namespace'."
    }

    $namespaceYaml | kubectl apply -f -
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create or update namespace '$Namespace'."
    }

    $setValues = @(
        "global.imageRegistry=$ImageRegistry",
        "global.imageTag=$ImageTag",
        "ingress.host=$Domain",
        "ingress.tls.secretName=microshop-api-tls",
        "apps.api-gateway.env.Gateway__AllowedCorsOrigins__0=https://$Domain"
    )

    if (-not [string]::IsNullOrWhiteSpace($ImagePullSecretName)) {
        $setValues += "global.imagePullSecrets[0].name=$ImagePullSecretName"
    }

    $setArguments = @()
    foreach ($value in $setValues) {
        $setArguments += "--set"
        $setArguments += $value
    }

    Invoke-RequiredCommand helm upgrade --install $ReleaseName $ChartPath `
        --namespace $Namespace `
        --create-namespace `
        --wait `
        --timeout $Timeout `
        @setArguments

    if (-not $SkipSmoke) {
        & (Join-Path $PSScriptRoot "k3s-smoke.ps1") -GatewayBaseUrl $GatewayBaseUrl -TimeoutSeconds 240
        if ($LASTEXITCODE -ne 0) {
            throw "K3s gateway smoke failed with exit code $LASTEXITCODE."
        }
    }

    if (-not $SkipObservabilitySmoke) {
        & (Join-Path $PSScriptRoot "k3s-observability-smoke.ps1") -Namespace $Namespace -TimeoutSeconds 240
        if ($LASTEXITCODE -ne 0) {
            throw "K3s observability smoke failed with exit code $LASTEXITCODE."
        }
    }

    Write-Host "K3s deploy completed. Release='$ReleaseName', Namespace='$Namespace', ImageTag='$ImageTag'."
}
catch {
    Write-Warning $_.Exception.Message

    if ($previousRevision) {
        Write-Warning "Deploy failed. Rolling back '$ReleaseName' to revision $previousRevision."
        helm rollback $ReleaseName $previousRevision --namespace $Namespace --wait --timeout $Timeout
    }
    else {
        Write-Warning "Deploy failed and no previous Helm revision was found. Manual cleanup may be required."
    }

    throw
}
