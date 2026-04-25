// Mission Control <-> Opencode.ai Zen Bridge
//
// Supports all opencode.ai AI SDK packages via correct API format per model:
//   "openai"            -> /v1/responses        (GPT models)
//   "anthropic"         -> /v1/messages         (Claude models)
//   "google"            -> /v1/models/{model}   (Gemini models)
//   "openai-compatible" -> /v1/chat/completions (Qwen, MiniMax, etc.)

import express from 'express';
import { randomUUID } from 'node:crypto';
import { readFileSync, existsSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { execSync } from 'node:child_process';

const __dirname = dirname(fileURLToPath(import.meta.url));

let config = { port: 4100 };
const configPath = join(__dirname, 'config.json');
if (existsSync(configPath)) try { config = { ...config, ...JSON.parse(readFileSync(configPath, 'utf8')) }; } catch {}

const PORT = config.port;
const app = express();
app.use(express.json({ limit: '2mb' }));
const runs = new Map();

app.get('/health', (req, res) => res.json({ ok: true }));

app.post('/run', async (req, res) => {
  const {
    taskName = 'unnamed', prompt, systemPrompt, model, cwd,
    allowedTools, maxTurns = 10, apiKey, apiEndpoint, aiSdkPackage = 'openai-compatible',
  } = req.body || {};

  if (!prompt || !apiKey) return res.status(400).json({ error: 'prompt and apiKey required' });

  const runId = randomUUID();
  const startedAt = Date.now();
  runs.set(runId, { status: 'running', startedAt, events: [] });
  const sdk = aiSdkPackage;
  const endpoint = (apiEndpoint || 'https://opencode.ai/zen/v1/chat/completions').replace(/\/+$/, '');
  console.log(`[run ${runId}] ${taskName} | model=${model} | sdk=${sdk} | ${endpoint}`);

  try {
    const transcript = [];
    let finalText = null;
    let totalUsage = { inputTokens: 0, outputTokens: 0, cacheReadInputTokens: 0, cacheCreationInputTokens: 0, costUsd: 0 };

    if (sdk === 'anthropic') {
      // --- Anthropic Messages API ---
      const messages = [];
      if (systemPrompt) messages.push({ role: 'user', content: systemPrompt });
      messages.push({ role: 'user', content: prompt });

      let msgResp;
      for (let turn = 0; turn < maxTurns; turn++) {
        console.log(`[run ${runId}] turn ${turn + 1} (anthropic)`);
        const resp = await fetch(endpoint, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'x-api-key': apiKey, 'anthropic-version': '2023-06-01' },
          body: JSON.stringify({ model, max_tokens: 8192, messages }),
        });
        if (!resp.ok) throw new Error(`API ${resp.status}: ${(await resp.text()).slice(0, 300)}`);
        const d = await resp.json();
        msgResp = d;
        totalUsage.inputTokens += d.usage?.input_tokens || 0;
        totalUsage.outputTokens += d.usage?.output_tokens || 0;
        transcript.push({ type: 'assistant', model, content: d.content, stop_reason: d.stop_reason, usage: d.usage });

        const textBlock = d.content?.find(b => b.type === 'text');
        const toolBlock = d.content?.find(b => b.type === 'tool_use');

        if (toolBlock) {
          messages.push({ role: 'assistant', content: d.content });
          const toolResult = executeTool({ name: toolBlock.name, arguments: JSON.stringify(toolBlock.input) }, cwd || '');
          messages.push({ role: 'user', content: [{ type: 'tool_result', tool_use_id: toolBlock.id, content: toolResult }] });
          transcript.push({ type: 'tool', name: toolBlock.name, result: toolResult.slice(0, 500) });
        } else if (textBlock) {
          finalText = textBlock.text;
          break;
        }
      }

    } else if (sdk === 'openai') {
      // --- OpenAI Responses API ---
      const input = [];
      if (systemPrompt) input.push({ role: 'system', content: systemPrompt });
      input.push({ role: 'user', content: prompt });

      for (let turn = 0; turn < maxTurns; turn++) {
        console.log(`[run ${runId}] turn ${turn + 1} (openai)`);
        const resp = await fetch(endpoint, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${apiKey}` },
          body: JSON.stringify({ model, input, max_output_tokens: 8192 }),
        });
        if (!resp.ok) throw new Error(`API ${resp.status}: ${(await resp.text()).slice(0, 300)}`);
        const d = await resp.json();
        totalUsage.inputTokens += d.usage?.input_tokens || 0;
        totalUsage.outputTokens += d.usage?.output_tokens || 0;
        const msg = d.output?.find(o => o.type === 'message');
        if (msg?.content?.[0]?.text) { finalText = msg.content[0].text; break; }
        transcript.push({ type: 'assistant', model, output: d.output, usage: d.usage });
        if (!d.output?.length) break;
      }

    } else if (sdk === 'google') {
      // --- Google Gemini API ---
      const contents = [];
      if (systemPrompt) contents.push({ role: 'user', parts: [{ text: systemPrompt }] });
      contents.push({ role: 'user', parts: [{ text: prompt }] });

      const url = `${endpoint}:generateContent`;
      const resp = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ contents, generationConfig: { maxOutputTokens: 8192 } }),
      });
      if (!resp.ok) throw new Error(`API ${resp.status}: ${(await resp.text()).slice(0, 300)}`);
      const d = await resp.json();
      finalText = d.candidates?.[0]?.content?.parts?.[0]?.text || null;
      totalUsage.inputTokens = d.usageMetadata?.promptTokenCount || 0;
      totalUsage.outputTokens = d.usageMetadata?.candidatesTokenCount || 0;
      transcript.push({ type: 'assistant', model, candidates: d.candidates, usage: d.usageMetadata });

    } else {
      // --- OpenAI Chat Completions (default) ---
      const messages = [];
      if (systemPrompt) messages.push({ role: 'system', content: systemPrompt });
      messages.push({ role: 'user', content: prompt });

      const tools = buildTools(allowedTools);

      for (let turn = 0; turn < maxTurns; turn++) {
        console.log(`[run ${runId}] turn ${turn + 1} (chat)`);
        const body = { model, messages, max_tokens: 8192 };
        if (tools.length) { body.tools = tools; body.tool_choice = 'auto'; }

        const resp = await fetch(endpoint, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${apiKey}` },
          body: JSON.stringify(body),
        });
        if (!resp.ok) throw new Error(`API ${resp.status}: ${(await resp.text()).slice(0, 300)}`);
        const d = await resp.json();
        const msg = d.choices?.[0]?.message;
        if (!msg) break;
        totalUsage.inputTokens += d.usage?.prompt_tokens || 0;
        totalUsage.outputTokens += d.usage?.completion_tokens || 0;
        transcript.push({ type: 'assistant', model, content: msg.content, finish_reason: d.choices[0].finish_reason, usage: d.usage });

        if (msg.tool_calls?.length) {
          messages.push({ role: 'assistant', content: msg.content || null, tool_calls: msg.tool_calls.filter(tc => tc.type === 'function').map(tc => ({ id: tc.id, type: 'function', function: tc.function })) });
          for (const tc of msg.tool_calls) {
            if (tc.type !== 'function') continue;
            const result = executeTool(tc.function, cwd || '');
            transcript.push({ type: 'tool', name: tc.function.name, result: result.slice(0, 500) });
            messages.push({ role: 'tool', tool_call_id: tc.id, content: result });
          }
        } else {
          finalText = msg.content || '';
          break;
        }
      }
    }

    if (!finalText) finalText = 'No response after max turns';
    const record = { runId, status: 'success', result: finalText, transcript: JSON.stringify(transcript, null, 2), error: null, durationMs: Date.now() - startedAt, usage: totalUsage };
    runs.set(runId, { ...runs.get(runId), ...record, status: 'success' });
    console.log(`[run ${runId}] done in ${record.durationMs}ms`);
    res.json(record);
  } catch (err) {
    console.error(`[run ${runId}] error:`, err.message);
    res.status(500).json({ runId, status: 'error', result: null, transcript: '[]', error: err.message, durationMs: Date.now() - startedAt, usage: null });
  }
});

app.get('/runs/:id', (req, res) => {
  const run = runs.get(req.params.id);
  if (!run) return res.status(404).json({ error: 'not found' });
  res.json(run);
});

app.listen(PORT, () => console.log(`zen-bridge on :${PORT} | opencode.ai Zen API`));

// --- Shared helpers ---
function buildTools(allowedTools) {
  const all = [
    { type: 'function', function: { name: 'read_file', description: 'Read file', parameters: { type: 'object', properties: { path: { type: 'string' } }, required: ['path'] } } },
    { type: 'function', function: { name: 'write_file', description: 'Write file', parameters: { type: 'object', properties: { path: { type: 'string' }, content: { type: 'string' } }, required: ['path', 'content'] } } },
    { type: 'function', function: { name: 'list_files', description: 'List directory', parameters: { type: 'object', properties: { path: { type: 'string' } }, required: ['path'] } } },
    { type: 'function', function: { name: 'search_content', description: 'Search text in files', parameters: { type: 'object', properties: { pattern: { type: 'string' }, path: { type: 'string' } }, required: ['pattern'] } } },
    { type: 'function', function: { name: 'run_command', description: 'Run shell command', parameters: { type: 'object', properties: { command: { type: 'string' } }, required: ['command'] } } },
  ];
  if (!allowedTools?.length) return all;
  const s = new Set(allowedTools.map(t => t.toLowerCase()));
  return all.filter(t => {
    const n = t.function.name;
    return (s.has('read') && n === 'read_file') || (s.has('write') && n === 'write_file') || (s.has('glob') && n === 'list_files') || (s.has('grep') && n === 'search_content') || (s.has('bash') && n === 'run_command');
  });
}

function executeTool(func, cwd) {
  try {
    const args = JSON.parse(func.arguments || '{}');
    switch (func.name) {
      case 'read_file': return readFileSync(join(cwd, args.path), 'utf8').slice(0, 50000);
      case 'write_file': { const p = join(cwd, args.path); const d = dirname(p); if (!existsSync(d)) execSync(`mkdir -p "${d}"`, { cwd, timeout: 3000 }); writeFileSync(p, args.content, 'utf8'); return `Written to ${args.path}`; }
      case 'list_files': try { return execSync(`ls -la "${join(cwd, args.path || '.')}"`, { cwd, timeout: 3000 }).toString(); } catch { return execSync(`powershell -Command "Get-ChildItem '${join(cwd, args.path || '.')}' | Format-Table -AutoSize"`, { cwd, timeout: 3000 }).toString(); }
      case 'search_content': try { return execSync(`grep -rl "${args.pattern}" "${join(cwd, args.path || '.')}"`, { cwd, timeout: 5000 }).toString() || 'No matches'; } catch { try { return execSync(`findstr /s /m "${args.pattern}" "${join(cwd, args.path || '.')}\\*"`, { cwd, timeout: 5000 }).toString() || 'No matches'; } catch { return 'No matches'; } }
      case 'run_command': return execSync(args.command, { cwd, timeout: 30000, maxBuffer: 1024 * 1024 }).toString();
      default: return `Unknown tool: ${func.name}`;
    }
  } catch (err) { return `Tool error: ${err.message}`; }
}