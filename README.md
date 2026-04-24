# Mission Control

Central dashboard to monitor, manage, and coordinate AI agent runs, workflows, and tasks across multiple providers.

## Architecture

```
┌────────────────────────────────────────────────────────────────────┐
│                    MissionControl.Web (C#)                         │
│                 Blazor Server + EF Core + SQLite                    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │  Providers  │  │   Models    │  │    Tasks    │              │
│  │ (Opencode,  │◄─┤ (per prov) │◄─┤ (assigns    │              │
│  │  Claude)    │  │            │  │  model)     │              │
│  └──────┬──────┘  └────────────┘  └─────────────┘              │
│         │                                                      │
│         │ HTTP                                                │
└─────────┼──────────────────────────────────────────────────────┘
          │                   
          ▼                   
┌────────────────────────────────────────────────────────────────────┐
│                         Provider Bridges                            │
│  ┌──────────────────┐  ┌──────────────────┐                      │
│  │  zen-bridge      │  │  claude-bridge   │                      │
│  │ (:4100, Opencode)│  │ (:4200, Claude)  │                      │
│  └──────────────────┘  └──────────────────┘                      │
│         ▲                     ▲                                   │
│         │                     │                                   │
└─────────┼─────────────────────┼───────────────────────────────────┘
          │                     │                                   
          ▼                     ▼                                   
┌────────────────────────────────────────────────────────────────────┐
│              Obsidian vault (filesystem)                         │
│  - knowledge base (agents read)                              │
│  - run transcripts (agents & C# write)                 │
└────────────────────────────────────────────────────┘
```

**Why the C#/Node split?** AI agent SDKs are JavaScript-only. A tiny Node.js sidecar per provider exposes it over localhost HTTP so the C# Blazor app stays the single source of truth for UI, scheduling, persistence, and secrets.

## Multi-Provider Support

Mission Control supports multiple AI providers simultaneously:
- **Opencode Zen** — uses `zen-bridge` on port 4100
- **Claude** — uses `claude-bridge` on port 4200

Each provider has:
- API key stored in the database (per provider)
- Base URL for the bridge
- Multiple models with different capabilities

## Project layout

```
Mission Control/
├── MissionControl.sln
├── MissionControl.Web/
│   ├── Program.cs
│   ├── appsettings.json                    # committed defaults
│   ├── appsettings.Local.json              # gitignored, YOU create this
│   ├── appsettings.Local.json.example      # template
│   ├── Models/
│   │   ├── Provider.cs                  # Provider entity
│   │   ├── Model.cs                      # Model entity  
│   │   ├── AgentTask.cs                  # Task entity (references Model)
│   │   └── AgentRun.cs                   # Run entity
│   ├── Data/  Services/  Components/
│   └── wwwroot/app.css
├── zen-bridge/                          # Opencode Zen bridge
│   ├── package.json
│   ├── server.js
│   └── config.json                     # bridge port only (:4100)
├── claude-bridge/                       # Claude bridge
│   ├── package.json
│   ├── server.js
│   └── config.json                     # bridge port only (:4200)
└── scripts/
    ├── run.ps1
    ├── run.cmd
    ├── run-linux.sh
    └── mission-control.service
```

## Prerequisites

- **.NET 8 SDK** — https://dotnet.microsoft.com/download
- **Node.js 20+** — https://nodejs.org
- An **Obsidian vault** directory (any folder with markdown files)

That's it. No environment variables to set.

## First-time setup

1. Copy the example config:
   - **Windows:** `copy MissionControl.Web\appsettings.Local.json.example MissionControl.Web\appsettings.Local.json`
   - **Linux/macOS:** `cp MissionControl.Web/appsettings.Local.json.example MissionControl.Web/appsettings.Local.json`

2. Open `MissionControl.Web/appsettings.Local.json` and fill in your vault path:
   ```json
   {
     "Obsidian": { "VaultPath": "C:\\path\\to\\your\\vault" }
   }
   ```

3. Start the app and configure providers in the **Providers** page:
   - Add a provider (e.g., "Opencode" or "Claude")
   - Set the API key for each provider
   - Add models for each provider

`appsettings.Local.json` is gitignored — your vault path stays out of the repo.

## Run

**Start all bridges + web app:**
```powershell
.\scripts\run.ps1
```

Or start bridges manually:
```bash
cd zen-bridge && npm install && npm start &
cd claude-bridge && npm install && npm start &
cd MissionControl.Web && dotnet run
```

Then open http://localhost:5000.

## Usage

1. **Providers Page** — Add AI providers and their API keys
2. **Tasks Page** — Create tasks and assign them to a model
3. **Run** — Execute a task; the appropriate bridge is selected based on the model's provider

## Production hosting on Linux

```bash
sudo cp -r "Mission Control" /opt/mission-control
sudo useradd --system --home /opt/mission-control missioncontrol
sudo chown -R missioncontrol:missioncontrol /opt/mission-control
sudo cp /opt/mission-control/scripts/mission-control.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now mission-control
```

The systemd unit intentionally carries no secrets; configure providers in the database.