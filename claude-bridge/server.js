// Mission Control <-> Claude Agent SDK bridge
//
// Exposes a tiny HTTP surface the C# Blazor app calls when it wants to run an agent:
//   POST /run       { taskName, prompt, systemPrompt, model, cwd, allowedTools, maxTurns, apiKey }
//   GET  /runs/:id  -> { status, result, transcript, ... }
//   GET  /health    -> 200 always (no env-var dependency; the C# app owns the API key)
//
// The Agent SDK is JS-only, so this sidecar is what actually spins up agent loops.
// It runs each agent with `cwd` pointed at the user's Obsidian vault, so the agent's
// Read/Write/Grep tools are scoped to the knowledge base.
//
// No environment variables required — the C# app sends the API key on each /run call.

import express from 'express';
import { randomUUID } from 'node:crypto';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { query } from '@anthropic-ai/claude-agent-sdk';

const __dirname = dirname(fileURLToPath(import.meta.url));

let config = { port: 4200 };
const configPath = join(__dirname, 'config.json');
if (existsSync(configPath)) {
  try {
    config = { ...config, ...JSON.parse(readFileSync(configPath, 'utf8')) };
  } catch (err) {
    console.warn(`Could not parse ${configPath}: ${err.message}. Using defaults.`);
  }
}

const PORT = config.port;
const app = express();
app.use(express.json({ limit: '2mb' }));

const runs = new Map();

app.get('/health', (req, res) => {
  res.json({ ok: true });
});

app.post('/run', async (req, res) => {
  const {
    taskName = 'unnamed',
    prompt,
    systemPrompt,
    model,
    cwd,
    allowedTools,
    maxTurns = 10,
    apiKey,
    apiEndpoint,
  } = req.body || {};

  if (!prompt || typeof prompt !== 'string') {
    return res.status(400).json({ error: 'prompt is required' });
  }
  if (!cwd || typeof cwd !== 'string') {
    return res.status(400).json({ error: 'cwd (vault path) is required' });
  }
  if (!apiKey || typeof apiKey !== 'string') {
    return res.status(400).json({ error: 'apiKey is required (set it in provider config)' });
  }

  const runId = randomUUID();
  const startedAt = Date.now();
  runs.set(runId, { status: 'running', startedAt, events: [] });

  console.log(`[run ${runId}] start: ${taskName} (model: ${model}${apiEndpoint ? `, url: ${apiEndpoint}` : ''})`);

  const previousKey = process.env.ANTHROPIC_API_KEY;
  const previousUrl = process.env.ANTHROPIC_BASE_URL;
  process.env.ANTHROPIC_API_KEY = apiKey;
  if (apiEndpoint) process.env.ANTHROPIC_BASE_URL = apiEndpoint;

  const options = {
    model,
    cwd,
    maxTurns,
    permissionMode: 'acceptEdits',
  };
  if (systemPrompt) options.systemPrompt = systemPrompt;
  if (Array.isArray(allowedTools) && allowedTools.length > 0) {
    options.allowedTools = allowedTools;
  }

  try {
    const transcript = [];
    let finalText = null;
    let totalUsage = { inputTokens: 0, outputTokens: 0, cacheReadInputTokens: 0, cacheCreationInputTokens: 0, costUsd: 0 };

    for await (const message of query({ prompt, options })) {
      transcript.push(message);
      runs.get(runId).events.push(message);

      if (message.type === 'assistant' && message.message?.content) {
        for (const block of message.message.content) {
          if (block.type === 'text') {
            console.log(`[run ${runId}] assistant: ${block.text.slice(0, 120)}`);
          }
        }
      }
      if (message.type === 'result') {
        finalText = message.result ?? null;
        if (message.usage) {
          totalUsage = {
            inputTokens: message.usage.input_tokens ?? 0,
            outputTokens: message.usage.output_tokens ?? 0,
            cacheReadInputTokens: message.usage.cache_read_input_tokens ?? 0,
            cacheCreationInputTokens: message.usage.cache_creation_input_tokens ?? 0,
            costUsd: message.usage.total_cost_usd ?? 0
          };
        }
      }
    }

    const record = {
      runId,
      status: 'success',
      result: finalText,
      transcript: JSON.stringify(transcript, null, 2),
      error: null,
      durationMs: Date.now() - startedAt,
      usage: totalUsage
    };
    runs.set(runId, { ...runs.get(runId), ...record, status: 'success' });
    console.log(`[run ${runId}] done in ${record.durationMs}ms`);
    res.json(record);
  } catch (err) {
    console.error(`[run ${runId}] error:`, err);
    const record = {
      runId,
      status: 'error',
      result: null,
      transcript: JSON.stringify(runs.get(runId)?.events ?? [], null, 2),
      error: err?.message || String(err),
      durationMs: Date.now() - startedAt,
      usage: null
    };
    runs.set(runId, { ...runs.get(runId), ...record, status: 'error' });
    res.status(500).json(record);
  } finally {
    if (previousKey === undefined) delete process.env.ANTHROPIC_API_KEY;
    else process.env.ANTHROPIC_API_KEY = previousKey;
    if (previousUrl === undefined) delete process.env.ANTHROPIC_BASE_URL;
    else process.env.ANTHROPIC_BASE_URL = previousUrl;
  }
});

app.get('/runs/:id', (req, res) => {
  const run = runs.get(req.params.id);
  if (!run) return res.status(404).json({ error: 'not found' });
  res.json(run);
});

app.listen(PORT, () => {
  console.log(`claude-bridge listening on http://localhost:${PORT}`);
  console.log('API key is expected in each /run request body (owned by the C# app).');
});