# Mission Control launcher for Windows (PowerShell)
# Usage:  .\scripts\run.ps1
#   - Starts opencode serve on port 4096
#   - Starts zen-bridge on port 4100
#   - Starts Blazor web app
#   - Ctrl-C stops all cleanly
# Requires: .NET 8 SDK, Bun (https://bun.sh).

$ErrorActionPreference = 'Stop'

$root      = Split-Path -Parent $PSScriptRoot
$zenBridgeDir = Join-Path $root 'zen-bridge'
$webDir    = Join-Path $root 'MissionControl.Web'

function Assert-Command($name, $hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "$name is not on PATH. $hint"
    }
}
$env:Path = "C:\Users\$env:USERNAME\.bun\bin;$env:Path"
Assert-Command 'bun'    'Install Bun from https://bun.sh'
Assert-Command 'dotnet' 'Install the .NET 8 SDK from https://dotnet.microsoft.com/download'

Write-Host "==> Starting opencode serve on port 4096..." -ForegroundColor Cyan
$opencode = Start-Process -FilePath 'bun' -ArgumentList 'x', 'opencode-ai', 'serve', '--port', '4096' -PassThru -NoNewWindow
Write-Host "    opencode PID=$($opencode.Id)"

Start-Sleep -Seconds 2

Write-Host "==> Starting zen-bridge on port 4100..." -ForegroundColor Cyan
$zenBridge = Start-Process -FilePath 'node' -ArgumentList 'server.js' -WorkingDirectory $zenBridgeDir -PassThru -NoNewWindow
Write-Host "    zen-bridge PID=$($zenBridge.Id)"

Start-Sleep -Seconds 1

try {
    Write-Host "==> Starting MissionControl.Web on http://localhost:5000" -ForegroundColor Cyan
    Push-Location $webDir
    try   { & dotnet run --no-launch-profile --urls "http://localhost:5000" }
    finally { Pop-Location }
}
finally {
    if ($opencode -and -not $opencode.HasExited) {
        Write-Host ""
        Write-Host "==> Shutting down opencode..." -ForegroundColor Cyan
        Stop-Process -Id $opencode.Id -Force -ErrorAction SilentlyContinue
    }
    if ($zenBridge -and -not $zenBridge.HasExited) {
        Write-Host "==> Shutting down zen-bridge..." -ForegroundColor Cyan
        Stop-Process -Id $zenBridge.Id -Force -ErrorAction SilentlyContinue
    }
}