using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MissionControl.Services;

/// <summary>
/// Typed HTTP client for the Node.js Claude Agent SDK sidecar.
/// The sidecar wraps @anthropic-ai/claude-agent-sdk and exposes a tiny REST surface.
/// </summary>
public class ClaudeBridgeClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ClaudeBridgeClient> _log;

    public ClaudeBridgeClient(HttpClient http, ILogger<ClaudeBridgeClient> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<BridgeRunResult> RunAsync(BridgeRunRequest req, CancellationToken ct = default)
    {
        _log.LogInformation("Dispatching run to bridge: task={Task}, cwd={Cwd}", req.TaskName, req.Cwd);

        using var resp = await _http.PostAsJsonAsync("/run", req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Bridge returned {(int)resp.StatusCode} {resp.StatusCode}: {body}");
        }

        var result = await resp.Content.ReadFromJsonAsync<BridgeRunResult>(cancellationToken: ct);
        return result ?? throw new InvalidOperationException("Bridge returned empty body.");
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync("/health", ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public record BridgeRunRequest(
    [property: JsonPropertyName("taskName")] string TaskName,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("systemPrompt")] string? SystemPrompt,
    [property: JsonPropertyName("cwd")] string Cwd,
    [property: JsonPropertyName("allowedTools")] string[]? AllowedTools,
    [property: JsonPropertyName("maxTurns")] int MaxTurns,
    [property: JsonPropertyName("apiKey")] string ApiKey
);

public record BridgeRunResult(
    [property: JsonPropertyName("runId")] string RunId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("result")] string? Result,
    [property: JsonPropertyName("transcript")] string? Transcript,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("durationMs")] long DurationMs
);
