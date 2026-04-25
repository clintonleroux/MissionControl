using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MissionControl.Services;

public interface IProviderBridge
{
    string ProviderType { get; }
    Task<BridgeRunResult> RunAsync(BridgeRunRequest req, CancellationToken ct = default);
    Task<bool> PingAsync(CancellationToken ct = default);
}

public record BridgeRunRequest(
    [property: JsonPropertyName("taskName")] string TaskName,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("systemPrompt")] string? SystemPrompt,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("cwd")] string Cwd,
    [property: JsonPropertyName("allowedTools")] string[]? AllowedTools,
    [property: JsonPropertyName("maxTurns")] int MaxTurns,
    [property: JsonPropertyName("apiKey")] string ApiKey,
    [property: JsonPropertyName("apiEndpoint")] string? ApiEndpoint,
    [property: JsonPropertyName("aiSdkPackage")] string? AiSdkPackage
);

public record BridgeUsage(
    [property: JsonPropertyName("inputTokens")] int InputTokens,
    [property: JsonPropertyName("outputTokens")] int OutputTokens,
    [property: JsonPropertyName("cacheReadInputTokens")] int CacheReadInputTokens,
    [property: JsonPropertyName("cacheCreationInputTokens")] int CacheCreationInputTokens,
    [property: JsonPropertyName("costUsd")] decimal CostUsd
);

public record BridgeRunResult(
    [property: JsonPropertyName("runId")] string RunId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("result")] string? Result,
    [property: JsonPropertyName("transcript")] string? Transcript,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("durationMs")] long DurationMs,
    [property: JsonPropertyName("usage")] BridgeUsage? Usage
);

public class ProviderBridge : IProviderBridge
{
    private readonly HttpClient _http;
    private readonly ILogger<ProviderBridge> _log;
    private readonly string _providerType;

    public string ProviderType => _providerType;

    public ProviderBridge(HttpClient http, ILogger<ProviderBridge> log, string providerType)
    {
        _http = http;
        _log = log;
        _providerType = providerType;
    }

    public async Task<BridgeRunResult> RunAsync(BridgeRunRequest req, CancellationToken ct = default)
    {
        _log.LogInformation("Dispatching run to {Provider}: task={Task}, cwd={Cwd}", _providerType, req.TaskName, req.Cwd);

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