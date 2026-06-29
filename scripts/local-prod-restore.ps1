[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,

    [string]$ComposeFile = "compose.local-prod.yml",
    [string]$EnvFile = ".env.local-prod",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$databases = @(
    "catalogdb",
    "orderingdb",
    "discountdb",
    "identitydb",
    "paymentdb"
)

$applicationServices = @(
    "reverse-proxy",
    "api-gateway",
    "catalogservice",
    "basketservice",
    "orderingservice",
    "discountservice",
    "identityservice",
    "paymentservice",
    "orderqueryservice",
    "projectionworker",
    "notificationworker"
)

function Invoke-Compose {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    & docker compose --env-file $EnvFile -f $ComposeFile @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-ServiceContainerId {
    param([string]$ServiceName)

    $containerId = & docker compose --env-file $EnvFile -f $ComposeFile ps -q $ServiceName
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($containerId)) {
        throw "Container for service '$ServiceName' was not found. Start local-prod first."
    }

    return $containerId.Trim()
}

if (-not (Test-Path $EnvFile)) {
    throw "Env file '$EnvFile' was not found. Copy .env.example to .env.local-prod and fill the values first."
}

if (-not (Test-Path $BackupPath)) {
    throw "Backup path '$BackupPath' was not found."
}

foreach ($database in $databases) {
    $databaseDump = Join-Path $BackupPath "$database.dump"
    if (-not (Test-Path $databaseDump)) {
        throw "Missing PostgreSQL dump '$databaseDump'."
    }
}

$mongoArchive = Join-Path $BackupPath "mongodb.archive.gz"
if (-not (Test-Path $mongoArchive)) {
    throw "Missing MongoDB archive '$mongoArchive'."
}

if (-not $Force) {
    Write-Warning "This restore is destructive. It drops and recreates PostgreSQL databases and drops MongoDB documents from the archive target."
    $confirmation = Read-Host "Type RESTORE to continue"
    if ($confirmation -ne "RESTORE") {
        throw "Restore cancelled."
    }
}

$postgresContainer = Get-ServiceContainerId "postgres"
$mongoContainer = Get-ServiceContainerId "mongodb"

Invoke-Compose stop @applicationServices

foreach ($database in $databases) {
    $localPath = Join-Path $BackupPath "$database.dump"
    $remotePath = "/tmp/microshop-$database.dump"

    & docker cp $localPath "${postgresContainer}:$remotePath"
    if ($LASTEXITCODE -ne 0) {
        throw "docker cp failed for PostgreSQL database '$database'."
    }

    $resetCommand = "PGPASSWORD=`"`$POSTGRES_PASSWORD`" psql -U `"`$POSTGRES_USER`" -d postgres -v ON_ERROR_STOP=1 -c `"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$database' AND pid <> pg_backend_pid();`" && PGPASSWORD=`"`$POSTGRES_PASSWORD`" dropdb -U `"`$POSTGRES_USER`" --if-exists `"$database`" && PGPASSWORD=`"`$POSTGRES_PASSWORD`" createdb -U `"`$POSTGRES_USER`" -O `"`$POSTGRES_USER`" `"$database`" && PGPASSWORD=`"`$POSTGRES_PASSWORD`" pg_restore -U `"`$POSTGRES_USER`" -d `"$database`" --clean --if-exists `"$remotePath`""

    Invoke-Compose exec -T postgres sh -c $resetCommand
    Invoke-Compose exec -T postgres rm -f $remotePath
}

$mongoRemotePath = "/tmp/microshop-mongodb.archive.gz"
& docker cp $mongoArchive "${mongoContainer}:$mongoRemotePath"
if ($LASTEXITCODE -ne 0) {
    throw "docker cp failed for MongoDB archive."
}

$mongoRestoreCommand = "mongorestore --archive=`"$mongoRemotePath`" --gzip --drop --username `"`$MONGO_INITDB_ROOT_USERNAME`" --password `"`$MONGO_INITDB_ROOT_PASSWORD`" --authenticationDatabase admin"
Invoke-Compose exec -T mongodb sh -c $mongoRestoreCommand
Invoke-Compose exec -T mongodb rm -f $mongoRemotePath

Invoke-Compose start @applicationServices

Write-Host "Restore completed from: $BackupPath"
