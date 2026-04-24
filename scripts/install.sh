#!/usr/bin/env bash
# Deprecated — dependency installation is now handled automatically by
# scripts/run-linux.sh (and scripts/run.ps1 on Windows) on first run.
# Kept as a thin shim so existing habits don't break.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
echo "Nothing to do — scripts/run-linux.sh auto-installs npm deps on first launch."
echo "Just run: \"$SCRIPT_DIR/run-linux.sh\""
