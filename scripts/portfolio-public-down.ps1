[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$statePath = Join-Path $env:TEMP "microshop-portfolio-tunnels.json"
if (-not (Test-Path -LiteralPath $statePath)) {
    Write-Host "No MicroShop portfolio tunnel state file was found."
    return
}

$state = Get-Content -Raw $statePath | ConvertFrom-Json
foreach ($tunnel in $state.Tunnels) {
    if ($tunnel.Runtime -eq "Docker") {
        $container = & docker ps -a --filter "name=^/$($tunnel.ContainerName)$" --format "{{.Names}}" 2>$null
        if ($container -eq $tunnel.ContainerName) {
            & docker stop $tunnel.ContainerName | Out-Null
            Write-Host "Stopped $($tunnel.Name) tunnel container '$($tunnel.ContainerName)'."
        }

        continue
    }

    $process = Get-Process -Id $tunnel.ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        continue
    }

    if ($process.ProcessName -notlike "cloudflared*") {
        Write-Warning "Skipping PID $($tunnel.ProcessId): it is no longer a cloudflared process."
        continue
    }

    Stop-Process -Id $tunnel.ProcessId -Force
    Write-Host "Stopped $($tunnel.Name) tunnel (PID $($tunnel.ProcessId))."
}

Remove-Item -LiteralPath $statePath -Force
Write-Host "MicroShop portfolio Cloudflare tunnel state cleared."