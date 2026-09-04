[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Require-Path {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Required repository path is missing: $Path"
    }
}

Require-Path 'Gateways/ApiGateway/ApiGateway.csproj'
Require-Path 'Workers/NotificationWorker/NotificationWorker.csproj'
Require-Path 'Workers/ProjectionWorker/ProjectionWorker.csproj'
Require-Path 'Directory.Build.props'
Require-Path 'Directory.Packages.props'

$legacyReferences = rg -n 'Services[\\/]ApiGateway|Services[\\/]NotificationWorker' `
    -g '!GiaoAn/**' `
    -g '!**/bin/**' `
    -g '!**/obj/**' `
    -g '!**/node_modules/**' .

if ($LASTEXITCODE -eq 0) {
    throw "Legacy deployable-unit paths are still referenced outside historical learning material:`n$legacyReferences"
}

if ($LASTEXITCODE -ne 1) {
    throw "Repository path scan failed with exit code $LASTEXITCODE."
}

$duplicatedProjectDefaults = rg -n '<Nullable>enable</Nullable>|<ImplicitUsings>enable</ImplicitUsings>' -g '*.csproj'
if ($LASTEXITCODE -eq 0) {
    throw "Compiler defaults belong in Directory.Build.props, not individual projects:`n$duplicatedProjectDefaults"
}

if ($LASTEXITCODE -ne 1) {
    throw "Project-default scan failed with exit code $LASTEXITCODE."
}

$trackedDatabaseArtifacts = git ls-files -- 'Services/**/*.db' 'Services/**/*.sqlite' 'Workers/**/*.db' 'Workers/**/*.sqlite'
if ($trackedDatabaseArtifacts) {
    throw "Local database artifacts must not be tracked:`n$trackedDatabaseArtifacts"
}

[xml]$centralPackages = Get-Content 'Directory.Packages.props' -Raw
$centralPackageIds = @($centralPackages.Project.ItemGroup.PackageVersion | ForEach-Object { $_.Include })

$missingCentralVersions = [System.Collections.Generic.List[string]]::new()
$inlineVersions = [System.Collections.Generic.List[string]]::new()

rg --files -g '*.csproj' | ForEach-Object {
    [xml]$project = Get-Content $_ -Raw
    @($project.Project.ItemGroup.PackageReference) | Where-Object { $_ } | ForEach-Object {
        if ($_.Version) {
            $inlineVersions.Add("$_ -> $($_.Include)")
        }

        if ($_.Include -and $_.Include -notin $centralPackageIds) {
            $missingCentralVersions.Add("$_ -> $($_.Include)")
        }
    }
}

if ($inlineVersions.Count -gt 0) {
    throw "Package versions must be centralized in Directory.Packages.props:`n$($inlineVersions -join "`n")"
}

if ($missingCentralVersions.Count -gt 0) {
    throw "Package references missing a central version:`n$($missingCentralVersions -join "`n")"
}

Write-Host 'Repository layout validation passed.' -ForegroundColor Green