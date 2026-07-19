[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,

    [string]$Namespace = "microshop",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function Invoke-Kubectl {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    & kubectl @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "kubectl $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Assert-SafeIdentifier {
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Description
    )

    if ($Value -notmatch '^[A-Za-z_][A-Za-z0-9_-]*$') {
        throw "$Description '$Value' contains unsupported characters."
    }
}

function Assert-BackupFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Backup file '$Path' was not found."
    }

    if ((Get-Item -LiteralPath $Path).Length -eq 0) {
        throw "Backup file '$Path' is empty."
    }
}

function Assert-BackupChecksum {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExpectedChecksum
    )

    if ($ExpectedChecksum -notmatch '^[a-fA-F0-9]{64}$') {
        throw "Backup manifest contains an invalid SHA-256 checksum for '$Path'."
    }

    $actualChecksum = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
    if (-not $actualChecksum.Equals($ExpectedChecksum, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Backup checksum mismatch for '$Path'."
    }
}

function Get-ManifestChecksum {
    param(
        [Parameter(Mandatory = $true)][object]$Checksums,
        [Parameter(Mandatory = $true)][string]$FileName
    )

    $property = $Checksums.PSObject.Properties[$FileName]
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
        throw "Backup manifest does not contain a checksum for '$FileName'."
    }

    return [string]$property.Value
}

function Wait-ForApplicationPodsToStop {
    param([Parameter(Mandatory = $true)][string[]]$DeploymentNames)

    $deadline = (Get-Date).AddMinutes(3)

    foreach ($deploymentName in $DeploymentNames) {
        do {
            $podNames = & kubectl get pods -n $Namespace -l "app.kubernetes.io/name=$deploymentName" -o name
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to inspect pods for deployment '$deploymentName'."
            }

            if (-not $podNames) {
                break
            }

            if ((Get-Date) -ge $deadline) {
                throw "Timed out waiting for application pods to stop."
            }

            Start-Sleep -Seconds 2
        } while ($true)
    }
}

$resolvedBackupPath = (Resolve-Path -LiteralPath $BackupPath -ErrorAction Stop).Path
$manifestPath = Join-Path $resolvedBackupPath "manifest.json"
Assert-BackupFile -Path $manifestPath

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$formatVersion = [int]$manifest.formatVersion
$databases = @($manifest.postgresDatabases)
$mongoDatabase = [string]$manifest.mongoDatabase
$mongoArchiveName = [string]$manifest.mongoArchive
$checksums = $manifest.checksums

if ($formatVersion -ne 1) {
    throw "Unsupported backup manifest format version '$formatVersion'."
}

if ($null -eq $checksums) {
    throw "Backup manifest does not contain file checksums."
}

if ($databases.Count -eq 0) {
    throw "Backup manifest does not list any PostgreSQL databases."
}

if ([string]::IsNullOrWhiteSpace($mongoDatabase)) {
    throw "Backup manifest does not define mongoDatabase. Create a new backup with the current k3s-backup.ps1 script."
}

if ([string]::IsNullOrWhiteSpace($mongoArchiveName) -or [System.IO.Path]::GetFileName($mongoArchiveName) -ne $mongoArchiveName) {
    throw "Backup manifest contains an invalid MongoDB archive name."
}

foreach ($database in $databases) {
    Assert-SafeIdentifier -Value $database -Description "PostgreSQL database name"
    $databaseFileName = "$database.dump"
    $databasePath = Join-Path $resolvedBackupPath $databaseFileName
    Assert-BackupFile -Path $databasePath
    Assert-BackupChecksum -Path $databasePath -ExpectedChecksum (Get-ManifestChecksum -Checksums $checksums -FileName $databaseFileName)
}

Assert-SafeIdentifier -Value $mongoDatabase -Description "MongoDB database name"
$mongoArchivePath = Join-Path $resolvedBackupPath $mongoArchiveName
Assert-BackupFile -Path $mongoArchivePath
Assert-BackupChecksum -Path $mongoArchivePath -ExpectedChecksum (Get-ManifestChecksum -Checksums $checksums -FileName $mongoArchiveName)

if (-not [string]::IsNullOrWhiteSpace([string]$manifest.namespace) -and $manifest.namespace -ne $Namespace) {
    Write-Warning "Backup was created from namespace '$($manifest.namespace)' and will be restored into '$Namespace'."
}

if (-not $Force) {
    Write-Warning "This restore is destructive. It replaces the selected PostgreSQL databases and MongoDB read database in namespace '$Namespace'."
    $confirmation = Read-Host "Type RESTORE to continue"
    if ($confirmation -ne "RESTORE") {
        throw "Restore cancelled."
    }
}

$deploymentsJson = & kubectl get deployments -n $Namespace -l "app.kubernetes.io/component=application" -o json
if ($LASTEXITCODE -ne 0) {
    throw "Failed to list MicroShop application deployments in namespace '$Namespace'."
}

$deployments = @(($deploymentsJson | ConvertFrom-Json).items | ForEach-Object {
    [pscustomobject]@{
        Name = $_.metadata.name
        Replicas = [int]$_.spec.replicas
    }
})

if ($deployments.Count -eq 0) {
    throw "No application deployments were found. Deploy the current Helm chart before restoring data."
}

$restoreId = [Guid]::NewGuid().ToString("N")
$scaledDeployments = [System.Collections.Generic.List[object]]::new()

try {
    foreach ($deployment in $deployments) {
        Invoke-Kubectl -Arguments @("scale", "deployment/$($deployment.Name)", "-n", $Namespace, "--replicas=0")
        $scaledDeployments.Add($deployment)
    }

    Wait-ForApplicationPodsToStop -DeploymentNames @($deployments.Name)

    Push-Location $resolvedBackupPath
    try {
        foreach ($database in $databases) {
            $remotePath = "/tmp/microshop-restore-$restoreId-$database.dump"
            Invoke-Kubectl -Arguments @("cp", ".\$database.dump", "${Namespace}/postgres-0:$remotePath")

            try {
                $restoreCommand = "PGPASSWORD=`"`$POSTGRES_PASSWORD`" psql -U `"`$POSTGRES_USER`" -d postgres -v ON_ERROR_STOP=1 -c `"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$database' AND pid <> pg_backend_pid();`" && PGPASSWORD=`"`$POSTGRES_PASSWORD`" dropdb -U `"`$POSTGRES_USER`" --if-exists `"$database`" && PGPASSWORD=`"`$POSTGRES_PASSWORD`" createdb -U `"`$POSTGRES_USER`" -O `"`$POSTGRES_USER`" `"$database`" && PGPASSWORD=`"`$POSTGRES_PASSWORD`" pg_restore -U `"`$POSTGRES_USER`" -d `"$database`" --exit-on-error --no-owner --no-privileges `"$remotePath`""
                Invoke-Kubectl -Arguments @("exec", "-n", $Namespace, "statefulset/postgres", "--", "sh", "-c", $restoreCommand)
            }
            finally {
                & kubectl exec -n $Namespace statefulset/postgres -- rm -f $remotePath
            }
        }

        $mongoRemotePath = "/tmp/microshop-restore-$restoreId-mongodb.archive.gz"
        Invoke-Kubectl -Arguments @("cp", ".\$mongoArchiveName", "${Namespace}/mongodb-0:$mongoRemotePath")

        try {
            $mongoRestoreCommand = "mongorestore --archive=`"$mongoRemotePath`" --gzip --drop --nsInclude `"$mongoDatabase.*`" --username `"`$MONGO_INITDB_ROOT_USERNAME`" --password `"`$MONGO_INITDB_ROOT_PASSWORD`" --authenticationDatabase admin"
            Invoke-Kubectl -Arguments @("exec", "-n", $Namespace, "statefulset/mongodb", "--", "sh", "-c", $mongoRestoreCommand)
        }
        finally {
            & kubectl exec -n $Namespace statefulset/mongodb -- rm -f $mongoRemotePath
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    foreach ($deployment in $scaledDeployments) {
        & kubectl scale "deployment/$($deployment.Name)" -n $Namespace "--replicas=$($deployment.Replicas)"
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to restore replica count for deployment '$($deployment.Name)'."
        }
    }
}

foreach ($deployment in $deployments) {
    Invoke-Kubectl -Arguments @("rollout", "status", "deployment/$($deployment.Name)", "-n", $Namespace, "--timeout=5m")
}

Write-Host "K3s restore completed from: $resolvedBackupPath"
