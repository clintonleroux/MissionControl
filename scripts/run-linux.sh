#!/usr/bin/env bash
# Mission Control launcher for Linux / macOS / WSL / Git Bash.
# Usage:  ./scripts/run-linux.sh
#   - Starts opencode serve on port 4096
#   - Starts zen-bridge on port 4100
#   - Starts Blazor web app
#   - Ctrl-C stops all cleanly
# Requires: .NET 8 SDK, Bun (https://bun.sh).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ZEN_BRIDGE="$ROOT/zen-bridge"
WEB_DIR="$ROOT/MissionControl.Web"

require() {
  command -v "$1" >/dev/null 2>&1 || { echo "ERROR: '$1' not on PATH. $2" >&2; exit 1; }
}
require bun    "Install Bun from https://bun.sh"
require dotnet "Install the .NET 8 SDK (https://dotnet.microsoft.com/download)"

echo "==> Starting opencode serve on port 4096..."
(bun x opencode-ai serve --port 4096) &
OPENCODE_PID=$!
echo "    opencode PID=$OPENCODE_PID"

sleep 2

echo "==> Starting zen-bridge on port 4100..."
(cd "$ZEN_BRIDGE" && node server.js) &
ZEN_PID=$!
echo "    zen-bridge PID=$ZEN_PID"

cleanup() {
  echo ""
  echo "==> Shutting down services..."
  kill "$OPENCODE_PID" "$ZEN_PID" 2>/dev/null || true
  wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

sleep 1

echo "==> Starting MissionControl.Web on http://localhost:5000"
cd "$WEB_DIR"
dotnet run --no-launch-profile --urls "http://0.0.0.0:5000"