[CmdletBinding()]
param(
    [string]$ComposeFile = "compose.local-prod.yml",
    [string]$EnvFile = ".env.local-prod",
    [string]$BackupRoot = "backups/local-prod",
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

$backupDirectory = Join-Path $BackupRoot $Timestamp
New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null

$postgresContainer = Get-ServiceContainerId "postgres"
$mongoContainer = Get-ServiceContainerId "mongodb"

foreach ($database in $databases) {
    $remotePath = "/tmp/microshop-$database.dump"
    $localPath = Join-Path $backupDirectory "$database.dump"
    $dumpCommand = "PGPASSWORD=`"`$POSTGRES_PASSWORD`" pg_dump -U `"`$POSTGRES_USER`" -Fc -d `"$database`" -f `"$remotePath`""

    Invoke-Compose exec -T postgres sh -c $dumpCommand
    & docker cp "${postgresContainer}:$remotePath" $localPath
    if ($LASTEXITCODE -ne 0) {
        throw "docker cp failed for PostgreSQL database '$database'."
    }

    Invoke-Compose exec -T postgres rm -f $remotePath
}

$mongoRemotePath = "/tmp/microshop-mongodb.archive.gz"
$mongoLocalPath = Join-Path $backupDirectory "mongodb.archive.gz"
$mongoDumpCommand = "mongodump --archive=`"$mongoRemotePath`" --gzip --username `"`$MONGO_INITDB_ROOT_USERNAME`" --password `"`$MONGO_INITDB_ROOT_PASSWORD`" --authenticationDatabase admin"

Invoke-Compose exec -T mongodb sh -c $mongoDumpCommand
& docker cp "${mongoContainer}:$mongoRemotePath" $mongoLocalPath
if ($LASTEXITCODE -ne 0) {
    throw "docker cp failed for MongoDB archive."
}

Invoke-Compose exec -T mongodb rm -f $mongoRemotePath

$manifest = [ordered]@{
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    composeFile = $ComposeFile
    envFile = $EnvFile
    postgresDatabases = $databases
    mongoArchive = "mongodb.archive.gz"
}

$manifest | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $backupDirectory "manifest.json") -Encoding UTF8

Write-Host "Backup created: $backupDirectory"
