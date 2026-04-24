# Mission Control launcher for Windows (PowerShell)
# Usage:  .\scripts\run.ps1
#   - Installs claude-bridge npm deps on first run
#   - Starts the Node bridge + Blazor web app together
#   - Ctrl-C stops both cleanly
# Requires: Node.js 20+, .NET 8 SDK. No environment variables needed — all
# config lives in MissionControl.Web\appsettings.Local.json.

$ErrorActionPreference = 'Stop'

$root      = Split-Path -Parent $PSScriptRoot
$bridgeDir = Join-Path $root 'claude-bridge'
$webDir    = Join-Path $root 'MissionControl.Web'
$localCfg  = Join-Path $webDir 'appsettings.Local.json'

# --- Preflight ------------------------------------------------------------

function Assert-Command($name, $hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "$name is not on PATH. $hint"
    }
}
Assert-Command 'node'   'Install Node.js 20+ from https://nodejs.org'
Assert-Command 'npm'    'npm ships with Node.js; reinstall Node if missing.'
Assert-Command 'dotnet' 'Install the .NET 8 SDK from https://dotnet.microsoft.com/download'

if (-not (Test-Path $localCfg)) {
    Write-Host ""
    Write-Host "First-time setup:" -ForegroundColor Yellow
    Write-Host "  $localCfg is missing."
    Write-Host "  Copy appsettings.Local.json.example to appsettings.Local.json,"
    Write-Host "  then set Obsidian:VaultPath and Anthropic:ApiKey inside it."
    Write-Host ""
    throw "Missing appsettings.Local.json"
}

# --- Install bridge deps on first run -------------------------------------

if (-not (Test-Path (Join-Path $bridgeDir 'node_modules'))) {
    Write-Host "==> Installing claude-bridge npm dependencies (first run)..." -ForegroundColor Cyan
    Push-Location $bridgeDir
    try   { & npm install }
    finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "npm install failed (exit $LASTEXITCODE)" }
}

# --- Start the bridge as a tracked background process ---------------------

Write-Host "==> Starting claude-bridge..." -ForegroundColor Cyan
$bridge = Start-Process -FilePath 'node' -ArgumentList 'server.js' `
    -WorkingDirectory $bridgeDir -PassThru -NoNewWindow
Write-Host "    claude-bridge PID=$($bridge.Id)"

# Give Express a moment to bind its port.
Start-Sleep -Seconds 1

try {
    Write-Host "==> Starting MissionControl.Web on http://localhost:5000" -ForegroundColor Cyan
    Push-Location $webDir
    try   { & dotnet run --no-launch-profile --urls "http://localhost:5000" }
    finally { Pop-Location }
}
finally {
    if ($bridge -and -not $bridge.HasExited) {
        Write-Host ""
        Write-Host "==> Shutting down claude-bridge..." -ForegroundColor Cyan
        Stop-Process -Id $bridge.Id -Force -ErrorAction SilentlyContinue
    }
}
