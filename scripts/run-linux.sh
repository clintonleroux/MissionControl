#!/usr/bin/env bash
# Mission Control launcher for Linux / macOS / WSL / Git Bash.
# Usage:  ./scripts/run-linux.sh
#   - Installs claude-bridge npm deps on first run
#   - Starts the Node bridge + Blazor web app together
#   - Ctrl-C stops both cleanly
# Requires: Node.js 20+, .NET 8 SDK. No environment variables needed — all
# config lives in MissionControl.Web/appsettings.Local.json.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BRIDGE_DIR="$ROOT/claude-bridge"
WEB_DIR="$ROOT/MissionControl.Web"
LOCAL_CFG="$WEB_DIR/appsettings.Local.json"

# --- Preflight ------------------------------------------------------------

require() {
  command -v "$1" >/dev/null 2>&1 || { echo "ERROR: '$1' not on PATH. $2" >&2; exit 1; }
}
require node   "Install Node.js 20+ (https://nodejs.org)"
require npm    "npm ships with Node.js."
require dotnet "Install the .NET 8 SDK (https://dotnet.microsoft.com/download)"

if [[ ! -f "$LOCAL_CFG" ]]; then
  echo ""
  echo "First-time setup:"
  echo "  $LOCAL_CFG is missing."
  echo "  Copy appsettings.Local.json.example to appsettings.Local.json,"
  echo "  then set Obsidian:VaultPath and Anthropic:ApiKey inside it."
  echo ""
  exit 1
fi

# --- Install bridge deps on first run -------------------------------------

if [[ ! -d "$BRIDGE_DIR/node_modules" ]]; then
  echo "==> Installing claude-bridge npm dependencies (first run)..."
  (cd "$BRIDGE_DIR" && npm install)
fi

# --- Start the bridge as a tracked background process ---------------------

echo "==> Starting claude-bridge..."
(cd "$BRIDGE_DIR" && node server.js) &
BRIDGE_PID=$!
echo "    claude-bridge PID=$BRIDGE_PID"

cleanup() {
  echo ""
  echo "==> Shutting down claude-bridge..."
  kill "$BRIDGE_PID" 2>/dev/null || true
  wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

# Give Express a moment to bind its port.
sleep 1

echo "==> Starting MissionControl.Web on http://localhost:5000"
cd "$WEB_DIR"
dotnet run --no-launch-profile --urls "http://0.0.0.0:5000"
