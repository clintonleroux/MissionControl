# Mission Control

Central dashboard to monitor, manage, and coordinate Claude Agent SDK runs, workflows, and tasks.

## Architecture

```
┌──────────────────────────────┐       ┌──────────────────────────┐
│  MissionControl.Web (C#)     │  HTTP │  claude-bridge (Node.js) │
│  Blazor Server + EF Core     │──────▶│  Claude Agent SDK        │
│  SQLite persistence          │       │  POST /run, /runs/:id    │
│  Owns ALL config (API key,   │       │  No env vars required    │
│   vault path, bridge URL)    │       │                          │
└──────────────┬───────────────┘       └────────────┬─────────────┘
               │                                    │
               │ reads & writes markdown            │ agent reads/writes
               ▼                                    ▼
        ┌──────────────────────────────────────────────────┐
        │              Obsidian vault (filesystem)         │
        │  - knowledge base (agents read)                  │
        │  - run transcripts (agents & C# write)           │
        └──────────────────────────────────────────────────┘
```

**Why the C#/Node split?** The Claude Agent SDK is JavaScript-only. A tiny Node.js sidecar exposes it over localhost HTTP so the C# Blazor app stays the single source of truth for UI, scheduling, persistence, and secrets. The bridge needs no config of its own — the C# app sends the API key on each `/run` call.

## Project layout

```
Mission Control/
├── MissionControl.sln
├── MissionControl.Web/
│   ├── Program.cs
│   ├── appsettings.json                    # committed defaults
│   ├── appsettings.Local.json              # gitignored, YOU create this
│   ├── appsettings.Local.json.example      # template
│   ├── Models/  Data/  Services/  Components/
│   └── wwwroot/app.css
├── claude-bridge/
│   ├── package.json
│   ├── server.js
│   └── config.json                         # bridge port only
├── scripts/
│   ├── run.ps1                             # Windows launcher (auto-installs deps)
│   ├── run.cmd                             # double-click wrapper for run.ps1
│   ├── run-linux.sh                        # Linux/macOS/WSL launcher (auto-installs deps)
│   └── mission-control.service             # systemd unit for production hosting
└── README.md
```

## Prerequisites

- **.NET 8 SDK** — https://dotnet.microsoft.com/download
- **Node.js 20+** — https://nodejs.org
- An **Obsidian vault** directory (any folder with markdown files)
- An **Anthropic API key** — https://console.anthropic.com

That's it. No environment variables to set.

## First-time setup

Both launchers need one file before they'll start.

1. Copy the example config:
   - **Windows:** `copy MissionControl.Web\appsettings.Local.json.example MissionControl.Web\appsettings.Local.json`
   - **Linux/macOS:** `cp MissionControl.Web/appsettings.Local.json.example MissionControl.Web/appsettings.Local.json`

2. Open `MissionControl.Web/appsettings.Local.json` and fill in:
   ```json
   {
     "Obsidian": { "VaultPath": "C:\\path\\to\\your\\vault" },
     "Anthropic": { "ApiKey": "sk-ant-..." }
   }
   ```
   (Forward slashes work on Windows too, if you prefer.)

`appsettings.Local.json` is gitignored — your secrets stay out of the repo.

## Run

The launcher scripts auto-install npm deps on first run and start both processes together. Ctrl-C stops both cleanly.

**Windows:**
```powershell
.\scripts\run.ps1
```
Or double-click `scripts\run.cmd`.

**Linux / macOS / WSL:**
```bash
./scripts/run-linux.sh
```

Then open http://localhost:5000.

## MVP end-to-end loop

1. Create a task in the UI (e.g. "Summarize today's notes").
2. Click **Run** — web posts to `claude-bridge` over HTTP, including your API key from config.
3. Bridge invokes the Agent SDK with the Obsidian vault path as `cwd`, so the agent's Read/Write/Grep tools are scoped to the vault.
4. Agent output streams back; C# persists the run + transcript to SQLite.
5. Transcript is also written as a markdown note into the vault (`_MissionControl/runs/{timestamp}-run-{id}.md`).

## Production hosting on Linux

```bash
sudo cp -r "Mission Control" /opt/mission-control
sudo useradd --system --home /opt/mission-control missioncontrol
sudo chown -R missioncontrol:missioncontrol /opt/mission-control
# Create /opt/mission-control/MissionControl.Web/appsettings.Local.json with your vault + key
sudo cp /opt/mission-control/scripts/mission-control.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now mission-control
```

The systemd unit intentionally carries no secrets; everything lives in `appsettings.Local.json` on disk.

## Extending

- **More agents:** add rows to `AgentTask` with different `SystemPrompt` values.
- **Scheduling:** add a `BackgroundService` in `MissionControl.Web` that polls for due tasks.
- **Multi-step workflows:** add a `Workflow` model with ordered `AgentTask` steps.
- **MCP / hooks / subagents:** the SDK auto-reads your `~/.claude/` directory, so anything you've already wired up for Claude Code is available to Mission Control's runs with no extra config.
