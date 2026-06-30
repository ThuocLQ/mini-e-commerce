[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://localhost:5027",
    [string]$OtelHealthUrl = "http://localhost:13133",
    [string]$PrometheusBaseUrl = "http://localhost:9090",
    [string]$GrafanaBaseUrl = "http://localhost:3000",
    [int]$TimeoutSeconds = 180,
    [int]$PollSeconds = 3,
    [switch]$SkipGatewaySmoke
)

$ErrorActionPreference = "Stop"

$GatewayBaseUrl = $GatewayBaseUrl.TrimEnd("/")
$OtelHealthUrl = $OtelHealthUrl.TrimEnd("/")
$PrometheusBaseUrl = $PrometheusBaseUrl.TrimEnd("/")
$GrafanaBaseUrl = $GrafanaBaseUrl.TrimEnd("/")

function Wait-HttpOk {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null

    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -UseBasicParsing -TimeoutSec 5

            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                Write-Host "[ok] $Url"
                return
            }

            $lastError = "HTTP $($response.StatusCode)"
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Seconds $PollSeconds
    }

    throw "Timed out waiting for $Url. Last error: $lastError"
}

Write-Host "Running MicroShop local-prod observability smoke..."

if (-not $SkipGatewaySmoke) {
    & (Join-Path $PSScriptRoot "local-prod-smoke.ps1") -GatewayBaseUrl $GatewayBaseUrl

    if ($LASTEXITCODE -ne 0) {
        throw "local-prod gateway smoke failed with exit code $LASTEXITCODE."
    }
}

Wait-HttpOk "$OtelHealthUrl/"
Wait-HttpOk "$PrometheusBaseUrl/-/ready"
Wait-HttpOk "$GrafanaBaseUrl/api/health"

$prometheusUp = Invoke-RestMethod `
    -Uri "$PrometheusBaseUrl/api/v1/query?query=up" `
    -Method Get `
    -TimeoutSec 10

if ($prometheusUp.status -ne "success") {
    throw "Prometheus query 'up' did not return success."
}

$targets = Invoke-RestMethod `
    -Uri "$PrometheusBaseUrl/api/v1/targets" `
    -Method Get `
    -TimeoutSec 10

if ($targets.status -ne "success") {
    throw "Prometheus targets API did not return success."
}

$activeJobs = @($targets.data.activeTargets | ForEach-Object { $_.labels.job } | Sort-Object -Unique)
$requiredJobs = @("otel-collector", "otel-collector-internal", "kafka-exporter")

foreach ($job in $requiredJobs) {
    if ($activeJobs -notcontains $job) {
        throw "Prometheus target job '$job' was not found. Active jobs: $($activeJobs -join ', ')"
    }

    Write-Host "[ok] Prometheus target job $job"
}

Write-Host "MicroShop local-prod observability smoke passed."
