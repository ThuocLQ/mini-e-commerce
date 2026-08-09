[CmdletBinding()]
param(
    [string]$BaseRef
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (git rev-parse --show-toplevel).Trim()
if (-not $repositoryRoot) {
    throw 'Unable to determine the Git repository root.'
}

Push-Location $repositoryRoot
try {
    function Get-RepositoryRelativePath {
        param([Parameter(Mandatory = $true)][string]$Path)

        $fullPath = [System.IO.Path]::GetFullPath($Path)
        $rootPath = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd([char]'\', [char]'/') + [System.IO.Path]::DirectorySeparatorChar
        if (-not $fullPath.StartsWith($rootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Path '$Path' is outside repository root '$repositoryRoot'."
        }

        return $fullPath.Substring($rootPath.Length).Replace([char]'\', [char]'/')
    }

    # These two files were committed before this guard existed. Keep the exception exact so a
    # future migration cannot reuse prefix 006 in OrderingService.
    $legacyDuplicateAllowlist = @{
        'Services/OrderingService/Infrastructure/Persistence/Migrations|6' = @(
            'Services/OrderingService/Infrastructure/Persistence/Migrations/006_AddOrderCurrency.sql',
            'Services/OrderingService/Infrastructure/Persistence/Migrations/006_AddOutboxCorrelationColumns.sql'
        )
    }

    $migrationPattern = '^(?<prefix>\d+)_.*\.sql$'
    $migrationDirectories = Get-ChildItem -Path (Join-Path $repositoryRoot 'Services') -Recurse -Directory -Filter Migrations
    $errors = [System.Collections.Generic.List[string]]::new()

    foreach ($directory in $migrationDirectories) {
        $relativeDirectory = Get-RepositoryRelativePath $directory.FullName
        $migrations = Get-ChildItem -Path $directory.FullName -File -Filter '*.sql' |
            ForEach-Object {
                if ($_.Name -match $migrationPattern) {
                    [pscustomobject]@{
                        Path = Get-RepositoryRelativePath $_.FullName
                        Prefix = [int]$Matches.prefix
                    }
                }
            }

        foreach ($group in ($migrations | Group-Object Prefix | Where-Object Count -gt 1)) {
            $allowlistKey = "$relativeDirectory|$($group.Name)"
            $actualPaths = @($group.Group.Path | Sort-Object)
            $allowedPaths = @($legacyDuplicateAllowlist[$allowlistKey] | Sort-Object)

            if ($allowedPaths.Count -gt 0 -and ($actualPaths -join "`n") -eq ($allowedPaths -join "`n")) {
                Write-Host "Baseline exception: duplicate prefix $($group.Name) in $relativeDirectory is allowed for the two committed legacy OrderingService migrations."
                continue
            }

            $errors.Add("Duplicate DbUp migration prefix $($group.Name) in ${relativeDirectory}: $($actualPaths -join ', ').")
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($BaseRef) -and $BaseRef -notmatch '^0+$') {
        git cat-file -e "$BaseRef^{commit}"
        if ($LASTEXITCODE -ne 0) {
            throw "Base ref '$BaseRef' is not available locally. Fetch the base commit before running this guard."
        }

        $newMigrationPaths = @(git diff --name-only --diff-filter=A "$BaseRef" HEAD -- Services |
            Where-Object { $_ -match '^Services/.+/Infrastructure/Persistence/Migrations/\d+_.+\.sql$' })

        foreach ($path in $newMigrationPaths) {
            $fileName = [System.IO.Path]::GetFileName($path)
            if ($fileName -notmatch $migrationPattern) {
                continue
            }

            $prefix = [int]$Matches.prefix
            $relativeDirectory = [System.IO.Path]::GetDirectoryName($path).Replace('\', '/')
            $baseMigrationPaths = @(git ls-tree -r --name-only $BaseRef -- $relativeDirectory |
                Where-Object { [System.IO.Path]::GetFileName($_) -match $migrationPattern })
            $basePrefixes = @($baseMigrationPaths | ForEach-Object {
                $null = [System.IO.Path]::GetFileName($_) -match $migrationPattern
                [int]$Matches.prefix
            })

            if ($basePrefixes.Count -gt 0) {
                $highestExistingPrefix = ($basePrefixes | Measure-Object -Maximum).Maximum
                if ($prefix -le $highestExistingPrefix) {
                    $errors.Add("New DbUp migration '$path' uses prefix $prefix, which must be greater than the base prefix $highestExistingPrefix in $relativeDirectory.")
                }
            }
        }
    }
    else {
        Write-Host 'No base ref was supplied; checked duplicate prefixes only. Monotonic prefix validation runs in CI when a base commit is available.'
    }

    if ($errors.Count -gt 0) {
        $errors | ForEach-Object { Write-Error $_ }
        throw "DbUp migration naming guard failed with $($errors.Count) error(s)."
    }

    Write-Host 'DbUp migration naming guard passed.'
}
finally {
    Pop-Location
}
