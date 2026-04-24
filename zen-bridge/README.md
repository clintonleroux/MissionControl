# zen-bridge

Tiny Node.js sidecar that exposes the [Opencode Zen SDK](https://opencode.com/docs/zen/sdk) to the Mission Control C# app over localhost HTTP.

## Why a sidecar?

The Agent SDK is JavaScript/TypeScript only. Rather than shelling out to a CLI and parsing output, we run the SDK directly in a minimal Express server and call it from C# via `HttpClient`. This keeps agent state management, tool permissions, and transcript streaming in one place.

## Run

```bash
npm install
export OPENCODE_API_KEY=sk-ant-...
npm start                 # listens on :4100
```

## Endpoints

### `POST /run`
```json
{
  "taskName": "Summarize notes",
  "prompt": "Read today's notes and produce a 5-bullet summary.",
  "systemPrompt": "You are a diligent note-keeper.",
  "cwd": "/home/user/vault",
  "allowedTools": ["Read", "Write", "Grep", "Glob"],
  "maxTurns": 10
}
```
Returns the full transcript + final result when the agent loop finishes.

### `GET /runs/:id`
Returns the current state of an in-flight or completed run.

### `GET /health`
200 if `OPENCODE_API_KEY` is set, 503 otherwise.

## Configuration

| Env var             | Default | Purpose                     |
|---------------------|---------|-----------------------------|
| `PORT`              | `4100`  | HTTP port                   |
| `OPENCODE_API_KEY` | —       | **Required** for agent runs |
