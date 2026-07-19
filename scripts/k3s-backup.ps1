[CmdletBinding()]
param(
    [string]$Namespace = "microshop",
    [string]$BackupRoot = "backups/k3s",
    [string]$MongoDatabase = "MicroShop_OrderReadDb",
    [string]$Timestamp = (Get-Date -Format "yyyyMMdd-HHmmss")
)

$ErrorActionPreference = "Stop"

if ($MongoDatabase -notmatch '^[A-Za-z0-9_-]+$') {
    throw "MongoDB database name '$MongoDatabase' contains unsupported characters."
}

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
$mongoDumpCommand = "mongodump --archive=`"$mongoRemotePath`" --gzip --db `"$MongoDatabase`" --username `"`$MONGO_INITDB_ROOT_USERNAME`" --password `"`$MONGO_INITDB_ROOT_PASSWORD`" --authenticationDatabase admin"

kubectl exec -n $Namespace statefulset/mongodb -- sh -c $mongoDumpCommand
if ($LASTEXITCODE -ne 0) {
    throw "Failed to dump MongoDB."
}

kubectl cp "${Namespace}/mongodb-0:$mongoRemotePath" $mongoLocalPath
if ($LASTEXITCODE -ne 0) {
    throw "Failed to copy MongoDB archive."
}

kubectl exec -n $Namespace statefulset/mongodb -- rm -f $mongoRemotePath

$checksums = [ordered]@{}
foreach ($database in $databases) {
    $fileName = "$database.dump"
    $checksums[$fileName] = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $backupDirectory $fileName)).Hash.ToLowerInvariant()
}

$checksums["mongodb.archive.gz"] = (Get-FileHash -Algorithm SHA256 -LiteralPath $mongoLocalPath).Hash.ToLowerInvariant()

$manifest = [ordered]@{
    formatVersion = 1
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    namespace = $Namespace
    postgresDatabases = $databases
    mongoDatabase = $MongoDatabase
    mongoArchive = "mongodb.archive.gz"
    checksums = $checksums
}

$manifest | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $backupDirectory "manifest.json") -Encoding UTF8

Write-Host "K3s backup created: $backupDirectory"
