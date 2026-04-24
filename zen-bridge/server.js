// Mission Control <-> Opencode bridge
//
// Connects to a running opencode server (run `opencode serve` first)
//   POST /run       { taskName, prompt, cwd, model, apiKey }
//   GET  /runs/:id  -> { status, result, transcript, ... }
//   GET  /health    -> 200

import express from 'express';
import { randomUUID } from 'node:crypto';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { createOpencodeClient } from '@opencode-ai/sdk';

const __dirname = dirname(fileURLToPath(import.meta.url));

let config = { port: 4100, opencodeUrl: 'http://localhost:4096' };
const configPath = join(__dirname, 'config.json');
if (existsSync(configPath)) {
  try {
    config = { ...config, ...JSON.parse(readFileSync(configPath, 'utf8')) };
  } catch (err) {
    console.warn(`Could not parse ${configPath}: ${err.message}. Using defaults.`);
  }
}

const PORT = config.port;
const OPENCODE_URL = config.opencodeUrl;
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
    cwd,
    apiKey,
  } = req.body || {};

  if (!prompt || typeof prompt !== 'string') {
    return res.status(400).json({ error: 'prompt is required' });
  }
  if (!cwd || typeof cwd !== 'string') {
    return res.status(400).json({ error: 'cwd (vault path) is required' });
  }

  const runId = randomUUID();
  const startedAt = Date.now();
  runs.set(runId, { status: 'running', startedAt, events: [] });

  console.log(`[run ${runId}] start: ${taskName} (cwd: ${cwd})`);

  try {
    const client = createOpencodeClient({ baseUrl: OPENCODE_URL });
    
    const response = await client.session.prompt({
      path: cwd,
      body: { prompt }
    });

    const record = {
      runId,
      status: 'success',
      result: response.message?.content || JSON.stringify(response),
      transcript: JSON.stringify(response, null, 2),
      error: null,
      durationMs: Date.now() - startedAt,
      usage: { inputTokens: 0, outputTokens: 0, cacheReadInputTokens: 0, cacheCreationInputTokens: 0, costUsd: 0 }
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
  }
});

app.get('/runs/:id', (req, res) => {
  const run = runs.get(req.params.id);
  if (!run) return res.status(404).json({ error: 'not found' });
  res.json(run);
});

app.listen(PORT, () => {
  console.log(`zen-bridge listening on http://localhost:${PORT}`);
  console.log(`Connecting to opencode at ${OPENCODE_URL}`);
  console.log('Make sure to run "opencode serve" first!');
});