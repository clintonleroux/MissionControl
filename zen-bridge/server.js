// Mission Control <-> Opencode.ai bridge
//
// Connects to opencode.ai's unified API (https://opencode.ai/zen/v1).
// Supports all models via OpenAI-compatible chat completions endpoint.
// Model IDs use format:  opencode/<model-id>
//
//   POST /run       { taskName, prompt, systemPrompt, model, cwd, allowedTools, maxTurns, apiKey, apiEndpoint }
//   GET  /runs/:id  -> { status, result, transcript, ... }
//   GET  /health    -> 200

import express from 'express';
import { randomUUID } from 'node:crypto';
import { readFileSync, existsSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { execSync } from 'node:child_process';

const __dirname = dirname(fileURLToPath(import.meta.url));

let config = { port: 4100 };
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
  if (!apiKey || typeof apiKey !== 'string') {
    return res.status(400).json({ error: 'apiKey is required (set it in provider config)' });
  }

  const runId = randomUUID();
  const startedAt = Date.now();
  runs.set(runId, { status: 'running', startedAt, events: [] });

  const apiUrl = (apiEndpoint || 'https://opencode.ai/zen/v1').replace(/\/+$/, '');
  console.log(`[run ${runId}] start: ${taskName} (model: ${model}, api: ${apiUrl})`);

  const transcript = [];
  const tools = buildTools(allowedTools);

  try {
    const messages = [];
    if (systemPrompt) messages.push({ role: 'system', content: systemPrompt });
    messages.push({ role: 'user', content: prompt });

    transcript.push({
      type: 'system', subtype: 'init',
      cwd, model, apiUrl,
      tools: tools.map(t => t.function.name),
    });

    let finalText = null;
    let totalUsage = { inputTokens: 0, outputTokens: 0, cacheReadInputTokens: 0, cacheCreationInputTokens: 0, costUsd: 0 };

    for (let turn = 0; turn < maxTurns; turn++) {
      const body = {
        model,
        messages,
        max_tokens: 8192,
      };
      if (tools.length > 0) {
        body.tools = tools;
        body.tool_choice = 'auto';
      }

      console.log(`[run ${runId}] turn ${turn + 1}`);

      const response = await fetch(`${apiUrl}/chat/completions`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${apiKey}`,
        },
        body: JSON.stringify(body),
      });

      if (!response.ok) {
        const errText = await response.text();
        throw new Error(`API error ${response.status}: ${errText.slice(0, 500)}`);
      }

      const data = await response.json();
      const choice = data.choices?.[0];
      const msg = choice?.message;

      if (!msg) throw new Error('No message in API response');

      const usage = data.usage || {};
      totalUsage.inputTokens += usage.prompt_tokens || 0;
      totalUsage.outputTokens += usage.completion_tokens || 0;

      transcript.push({
        type: 'assistant',
        model: data.model || model,
        content: msg.content || msg,
        finish_reason: choice.finish_reason,
        usage,
      });

      if (msg.tool_calls?.length > 0) {
        messages.push({
          role: 'assistant',
          content: msg.content || null,
          tool_calls: msg.tool_calls.filter(tc => tc.type === 'function').map(tc => ({
            id: tc.id, type: 'function', function: tc.function,
          })),
        });

        for (const tc of msg.tool_calls) {
          if (tc.type !== 'function') continue;
          const result = executeTool(tc.function, cwd || '');
          transcript.push({ type: 'tool', name: tc.function.name, result: result.slice(0, 500) });
          messages.push({ role: 'tool', tool_call_id: tc.id, content: result });
        }
        continue;
      }

      finalText = msg.content || '';
      break;
    }

    if (!finalText) finalText = 'No response after max turns';

    const record = {
      runId, status: 'success',
      result: finalText,
      transcript: JSON.stringify(transcript, null, 2),
      error: null,
      durationMs: Date.now() - startedAt,
      usage: totalUsage,
    };
    runs.set(runId, { ...runs.get(runId), ...record, status: 'success' });
    console.log(`[run ${runId}] done in ${record.durationMs}ms`);
    res.json(record);
  } catch (err) {
    console.error(`[run ${runId}] error:`, err.message);
    const record = {
      runId, status: 'error',
      result: null,
      transcript: JSON.stringify(transcript, null, 2),
      error: err?.message || String(err),
      durationMs: Date.now() - startedAt,
      usage: null,
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
  console.log('Connecting to opencode.ai Zen API (https://opencode.ai/zen/v1)');
});

// --- Tools ---

function buildTools(allowedTools) {
  const all = [
    { type: 'function', function: { name: 'read_file', description: 'Read file contents', parameters: { type: 'object', properties: { path: { type: 'string' } }, required: ['path'] } } },
    { type: 'function', function: { name: 'write_file', description: 'Write content to file', parameters: { type: 'object', properties: { path: { type: 'string' }, content: { type: 'string' } }, required: ['path', 'content'] } } },
    { type: 'function', function: { name: 'list_files', description: 'List directory contents', parameters: { type: 'object', properties: { path: { type: 'string' } }, required: ['path'] } } },
    { type: 'function', function: { name: 'search_content', description: 'Search files for text', parameters: { type: 'object', properties: { pattern: { type: 'string' }, path: { type: 'string' } }, required: ['pattern'] } } },
    { type: 'function', function: { name: 'run_command', description: 'Run shell command', parameters: { type: 'object', properties: { command: { type: 'string' } }, required: ['command'] } } },
  ];
  if (!allowedTools?.length) return all;
  const allowed = new Set(allowedTools.map(t => t.toLowerCase()));
  return all.filter(t => {
    const n = t.function.name;
    return (allowed.has('read') && n === 'read_file') || (allowed.has('write') && n === 'write_file') || (allowed.has('glob') && n === 'list_files') || (allowed.has('grep') && n === 'search_content') || (allowed.has('bash') && n === 'run_command');
  });
}

function executeTool(func, cwd) {
  try {
    const args = JSON.parse(func.arguments || '{}');
    switch (func.name) {
      case 'read_file': return readFileSync(join(cwd, args.path), 'utf8').slice(0, 50000);
      case 'write_file': {
        const p = join(cwd, args.path);
        const d = dirname(p);
        if (!existsSync(d)) execSync(`mkdir -p "${d}"`, { cwd, timeout: 3000 });
        writeFileSync(p, args.content, 'utf8');
        return `Written to ${args.path}`;
      }
      case 'list_files': try { return execSync(`ls -la "${join(cwd, args.path || '.')}"`, { cwd, timeout: 3000 }).toString(); } catch { return execSync(`powershell -Command "Get-ChildItem '${join(cwd, args.path || '.')}' | Format-Table -AutoSize"`, { cwd, timeout: 3000 }).toString(); }
      case 'search_content': try { return execSync(`grep -r -l "${args.pattern}" "${join(cwd, args.path || '.')}"`, { cwd, timeout: 5000 }).toString() || 'No matches'; } catch { try { return execSync(`findstr /s /m "${args.pattern}" "${join(cwd, args.path || '.')}\\*"`, { cwd, timeout: 5000 }).toString() || 'No matches'; } catch { return 'No matches'; } }
      case 'run_command': return execSync(args.command, { cwd, timeout: 30000, maxBuffer: 1024 * 1024 }).toString();
      default: return `Unknown tool: ${func.name}`;
    }
  } catch (err) {
    return `Tool error: ${err.message}`;
  }
}