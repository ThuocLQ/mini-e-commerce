[CmdletBinding()]
param(
    [string]$Namespace = "microshop",
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

function Invoke-Kubectl {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    kubectl @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "kubectl $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Invoke-ClusterHttpCheck {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Url
    )

    $podName = "microshop-smoke-$($Name.ToLowerInvariant())-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"

    Invoke-Kubectl run $podName `
        --namespace $Namespace `
        --image curlimages/curl:8.10.1 `
        --restart Never `
        --rm `
        --quiet `
        --command `
        -- curl -fsS $Url

    Write-Host "[ok] $Name $Url"
}

Write-Host "Checking K3s observability rollout in namespace '$Namespace'..."

$deployments = @(
    "otel-collector",
    "prometheus",
    "grafana",
    "kafka-exporter"
)

foreach ($deployment in $deployments) {
    Invoke-Kubectl rollout status "deployment/$deployment" --namespace $Namespace "--timeout=${TimeoutSeconds}s"
}

Invoke-ClusterHttpCheck -Name "OtelCollector" -Url "http://otel-collector:13133/"
Invoke-ClusterHttpCheck -Name "PrometheusReady" -Url "http://prometheus:9090/-/ready"
Invoke-ClusterHttpCheck -Name "PrometheusQuery" -Url "http://prometheus:9090/api/v1/query?query=up"
Invoke-ClusterHttpCheck -Name "PrometheusRules" -Url "http://prometheus:9090/api/v1/rules?type=alert"
Invoke-ClusterHttpCheck -Name "GrafanaHealth" -Url "http://grafana:3000/api/health"

Write-Host "MicroShop K3s observability smoke passed."
