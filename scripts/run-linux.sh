#!/usr/bin/env bash
# Mission Control launcher for Linux / macOS / WSL / Git Bash.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ZEN_BRIDGE="$ROOT/zen-bridge"
CLAUDE_BRIDGE="$ROOT/claude-bridge"
WEB_DIR="$ROOT/MissionControl.Web"

require() { command -v "$1" >/dev/null 2>&1 || { echo "ERROR: '$1' not on PATH. $2" >&2; exit 1; }; }
require dotnet "Install .NET 8 SDK" && require node "Install Node.js 20+"

flush_port() { local pid=$(lsof -ti ":$1" 2>/dev/null || true); [ -n "$pid" ] && kill -9 $pid 2>/dev/null || true; }
for p in 4100 4200 5000; do flush_port "$p"; done; sleep 1

cleanup() { echo ""; echo "==> Shutting down..."; kill "$ZEN_PID" "$CLAUDE_PID" 2>/dev/null || true; wait 2>/dev/null || true; }
trap cleanup EXIT INT TERM

echo "==> Starting zen-bridge (opencode.ai) on port 4100..."
(cd "$ZEN_BRIDGE" && node server.js) & ZEN_PID=$!; sleep 1

echo "==> Starting claude-bridge on port 4200..."
(cd "$CLAUDE_BRIDGE" && node server.js) & CLAUDE_PID=$!; sleep 1

echo "==> Starting MissionControl.Web on http://localhost:5000"
cd "$WEB_DIR" && dotnet run --no-launch-profile --urls "http://0.0.0.0:5000"