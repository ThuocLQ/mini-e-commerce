[CmdletBinding()]
param(
    [string]$EnvFile = ".env.local-prod",
    [string]$GatewayBaseUrl = "http://localhost:5027",
    [switch]$BuildImages,
    [switch]$WithObservability,
    [switch]$SkipDotnetTests,
    [switch]$SkipDockerUp,
    [switch]$CreateBackup
)

$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Name"
    & $Action
    Write-Host "[ok] $Name"
}

function Assert-LastExitCode {
    param([string]$CommandName)

    if ($LASTEXITCODE -ne 0) {
        throw "$CommandName failed with exit code $LASTEXITCODE."
    }
}

$scripts = @(
    "scripts/local-prod-backup.ps1",
    "scripts/local-prod-restore.ps1",
    "scripts/local-prod-observability-smoke.ps1",
    "scripts/local-prod-observability-up.ps1",
    "scripts/local-prod-rc-verify.ps1",
    "scripts/local-prod-smoke.ps1",
    "scripts/local-prod-up.ps1",
    "scripts/test-kafka-lesson25.ps1"
)

Invoke-Step "Validate PowerShell scripts" {
    foreach ($script in $scripts) {
        $errors = $null
        [System.Management.Automation.PSParser]::Tokenize((Get-Content $script -Raw), [ref]$errors) | Out-Null

        if ($errors) {
            $errors | Format-List
            throw "PowerShell parser errors found in $script."
        }
    }
}

Invoke-Step "Build solution" {
    dotnet build MicroShop.sln --no-restore --nologo -v minimal
    Assert-LastExitCode "dotnet build"
}

if (-not $SkipDotnetTests) {
    Invoke-Step "Run integration tests" {
        dotnet test Tests/MicroShop.IntegrationTests/MicroShop.IntegrationTests.csproj --no-build --nologo -v minimal
        Assert-LastExitCode "dotnet test"
    }
}

Invoke-Step "Validate local-prod compose" {
    docker compose --env-file .env.example -f compose.local-prod.yml config --quiet
    Assert-LastExitCode "docker compose config"
}

Invoke-Step "Validate local-prod observability compose" {
    docker compose --env-file .env.example -f compose.local-prod.yml -f compose.observability.yml config --quiet
    Assert-LastExitCode "docker compose observability config"
}

if (-not $SkipDockerUp) {
    if (-not (Test-Path -Path $EnvFile)) {
        throw "Missing $EnvFile. Copy .env.example to $EnvFile and replace every CHANGEME value first."
    }

    if ($WithObservability) {
        Invoke-Step "Start local-prod with observability and run smoke" {
            $arguments = @("-EnvFile", $EnvFile, "-GatewayBaseUrl", $GatewayBaseUrl)

            if ($BuildImages) {
                $arguments += "-Build"
            }

            & (Join-Path $PSScriptRoot "local-prod-observability-up.ps1") @arguments
            Assert-LastExitCode "local-prod-observability-up"
        }
    }
    else {
        Invoke-Step "Start local-prod and run smoke" {
            $arguments = @("-EnvFile", $EnvFile, "-GatewayBaseUrl", $GatewayBaseUrl)

            if ($BuildImages) {
                $arguments += "-Build"
            }

            & (Join-Path $PSScriptRoot "local-prod-up.ps1") @arguments
            Assert-LastExitCode "local-prod-up"
        }
    }

    if ($CreateBackup) {
        Invoke-Step "Create local-prod backup" {
            & (Join-Path $PSScriptRoot "local-prod-backup.ps1") -EnvFile $EnvFile
            Assert-LastExitCode "local-prod-backup"
        }
    }
}

Write-Host ""
Write-Host "MicroShop local-prod release candidate verification completed."
