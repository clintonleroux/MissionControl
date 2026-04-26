# Mission Control launcher for Windows (PowerShell)
# Usage:  .\scripts\run.ps1
# Services started:
#   1. zen-bridge      on port 4100 (opencode.ai API)
#   2. claude-bridge   on port 4200 (Claude Agent SDK)
#   3. Blazor web app  on http://localhost:5000
# Ctrl-C stops all cleanly.
# Requires: .NET 8 SDK.

$ErrorActionPreference = 'Stop'

$root            = Split-Path -Parent $PSScriptRoot
$zenBridgeDir    = Join-Path $root 'zen-bridge'
$claudeBridgeDir = Join-Path $root 'claude-bridge'
$webDir          = Join-Path $root 'MissionControl.Web'

function Assert-Command($name, $hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) { throw "$name is not on PATH. $hint" }
}

function Flush-Port($port) {
    $proc = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($p in $proc) { if ($p -gt 0) { try { Stop-Process -Id $p -Force -ErrorAction SilentlyContinue } catch {} } }
}

Assert-Command 'node'   'Install Node.js 20+ from https://nodejs.org'
Assert-Command 'dotnet' 'Install the .NET 8 SDK from https://dotnet.microsoft.com/download'

Write-Host "Cleaning up ports 4100, 4200, 5000..." -ForegroundColor DarkGray
Flush-Port 4100; Flush-Port 4200; Flush-Port 5000
Start-Sleep -Seconds 1

Write-Host "==> Starting zen-bridge (opencode.ai) on port 4100..." -ForegroundColor Cyan
$zenBridge = Start-Process -FilePath 'node' -ArgumentList 'server.js' -WorkingDirectory $zenBridgeDir -PassThru -NoNewWindow
Write-Host "    zen-bridge PID=$($zenBridge.Id)"
Start-Sleep -Seconds 1

Write-Host "==> Starting claude-bridge on port 4200..." -ForegroundColor Cyan
$claudeBridge = Start-Process -FilePath 'node' -ArgumentList 'server.js' -WorkingDirectory $claudeBridgeDir -PassThru -NoNewWindow
Write-Host "    claude-bridge PID=$($claudeBridge.Id)"
Start-Sleep -Seconds 1

try {
    Write-Host "==> Starting MissionControl.Web on http://localhost:5000" -ForegroundColor Cyan
    Push-Location $webDir
    try   { & dotnet run -c Release --no-launch-profile --urls "http://localhost:5000" }
    finally { Pop-Location }
}
finally {
    Write-Host ""; foreach ($proc in @($zenBridge, $claudeBridge)) { if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } }
    Write-Host "All services stopped." -ForegroundColor DarkGray
}