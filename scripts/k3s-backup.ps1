[CmdletBinding()]
param(
    [string]$Namespace = "microshop",
    [string]$BackupRoot = "backups/k3s",
    [string]$Timestamp = (Get-Date -Format "yyyyMMdd-HHmmss")
)

$ErrorActionPreference = "Stop"

$databases = @(
    "catalogdb",
    "orderingdb",
    "discountdb",
    "identitydb",
    "paymentdb"
)

$backupDirectory = Join-Path $BackupRoot $Timestamp
New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null

foreach ($database in $databases) {
    $remotePath = "/tmp/microshop-$database.dump"
    $localPath = Join-Path $backupDirectory "$database.dump"
    $dumpCommand = "PGPASSWORD=`"`$POSTGRES_PASSWORD`" pg_dump -U `"`$POSTGRES_USER`" -Fc -d `"$database`" -f `"$remotePath`""

    kubectl exec -n $Namespace statefulset/postgres -- sh -c $dumpCommand
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to dump PostgreSQL database '$database'."
    }

    kubectl cp "${Namespace}/postgres-0:$remotePath" $localPath
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to copy PostgreSQL dump '$database'."
    }

    kubectl exec -n $Namespace statefulset/postgres -- rm -f $remotePath
}

$mongoRemotePath = "/tmp/microshop-mongodb.archive.gz"
$mongoLocalPath = Join-Path $backupDirectory "mongodb.archive.gz"
$mongoDumpCommand = "mongodump --archive=`"$mongoRemotePath`" --gzip --username `"`$MONGO_INITDB_ROOT_USERNAME`" --password `"`$MONGO_INITDB_ROOT_PASSWORD`" --authenticationDatabase admin"

kubectl exec -n $Namespace statefulset/mongodb -- sh -c $mongoDumpCommand
if ($LASTEXITCODE -ne 0) {
    throw "Failed to dump MongoDB."
}

kubectl cp "${Namespace}/mongodb-0:$mongoRemotePath" $mongoLocalPath
if ($LASTEXITCODE -ne 0) {
    throw "Failed to copy MongoDB archive."
}

kubectl exec -n $Namespace statefulset/mongodb -- rm -f $mongoRemotePath

$manifest = [ordered]@{
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    namespace = $Namespace
    postgresDatabases = $databases
    mongoArchive = "mongodb.archive.gz"
}

$manifest | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $backupDirectory "manifest.json") -Encoding UTF8

Write-Host "K3s backup created: $backupDirectory"
