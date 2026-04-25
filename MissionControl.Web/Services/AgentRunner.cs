using Microsoft.EntityFrameworkCore;
using MissionControl.Data;
using MissionControl.Models;
using MissionControl.Models.Enums;

namespace MissionControl.Services;

public class AgentRunner
{
    private readonly IDbContextFactory<MissionControlDb> _dbFactory;
    private readonly IProviderBridgeRegistry _bridgeRegistry;
    private readonly ObsidianVaultService _vault;
    private readonly ILogger<AgentRunner> _log;

    public AgentRunner(
        IDbContextFactory<MissionControlDb> dbFactory,
        IProviderBridgeRegistry bridgeRegistry,
        ObsidianVaultService vault,
        ILogger<AgentRunner> log)
    {
        _dbFactory = dbFactory;
        _bridgeRegistry = bridgeRegistry;
        _vault = vault;
        _log = log;
    }

    public async Task<int> StartAsync(int taskId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var task = await db.AgentTasks
            .Include(t => t.Model)
            .ThenInclude(m => m!.Provider)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found.");

        var run = new AgentRun
        {
            AgentTaskId = task.Id,
            Status = AgentRunStatus.Queued,
            StartedAt = DateTime.UtcNow
        };
        db.AgentRuns.Add(run);
        await db.SaveChangesAsync(ct);

        _ = Task.Run(() => ExecuteAsync(run.Id), CancellationToken.None);
        return run.Id;
    }

    private async Task ExecuteAsync(int runId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var run = await db.AgentRuns
            .Include(r => r.AgentTask)
            .ThenInclude(t => t!.Model!)
            .ThenInclude(m => m!.Provider)
            .FirstOrDefaultAsync(r => r.Id == runId);

        if (run is null)
        {
            _log.LogError("Run {RunId} not found.", runId);
            return;
        }

        var task = run.AgentTask;
        if (task is null || task.Model is null || task.Model.Provider is null)
        {
            _log.LogError("Run {RunId} has no model/provider assigned.", runId);
            run.Status = AgentRunStatus.Failed;
            run.ErrorMessage = "No model assigned to task.";
            await db.SaveChangesAsync();
            return;
        }

        var model = task.Model;
        var provider = model.Provider;

        run.Status = AgentRunStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        try
        {
            if (string.IsNullOrWhiteSpace(provider.ApiKey))
            {
                throw new InvalidOperationException(
                    $"API key not set for provider '{provider.Name}'. Please configure it in Settings.");
            }

            var allowedTools = string.IsNullOrWhiteSpace(task.AllowedTools)
                ? null
                : task.AllowedTools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var bridge = _bridgeRegistry.GetBridge(provider);
            if (bridge is null)
            {
                _log.LogError("No bridge registered for provider {Provider}.", provider.Name);
                run.Status = AgentRunStatus.Failed;
                run.ErrorMessage = $"No bridge for {provider.Name}.";
                await db.SaveChangesAsync();
                return;
            }

            var req = new BridgeRunRequest(
                TaskName: task.Name,
                Prompt: task.Prompt,
                SystemPrompt: task.SystemPrompt,
                Model: model.ProviderModelId,
                Cwd: _vault.VaultRoot,
                AllowedTools: allowedTools,
                MaxTurns: task.MaxTurns,
                ApiKey: provider.ApiKey,
                ApiEndpoint: model.ApiEndpoint ?? provider.ApiEndpoint,
                AiSdkPackage: model.AiSdkPackage
            );

            var result = await bridge.RunAsync(req);

            run.BridgeRunId = result.RunId;
            run.Result = result.Result;
            run.Transcript = result.Transcript;
            run.Status = result.Status == "success" ? AgentRunStatus.Succeeded : AgentRunStatus.Failed;
            run.ErrorMessage = result.Error;
            run.CompletedAt = DateTime.UtcNow;

            if (result.Usage is not null)
            {
                run.InputTokens = result.Usage.InputTokens;
                run.OutputTokens = result.Usage.OutputTokens;
                run.CacheReadTokens = result.Usage.CacheReadInputTokens;
                run.CacheCreationTokens = result.Usage.CacheCreationInputTokens;
                run.CostUsd = result.Usage.CostUsd;
            }

            await UpdateProviderUsageAsync(provider, result.Usage);
            run.VaultNotePath = await _vault.WriteRunNoteAsync(task, run);
            await db.SaveChangesAsync();
            _log.LogInformation("Run {RunId} finished: {Status}", run.Id, run.Status);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Run {RunId} threw.", run.Id);
            run.Status = AgentRunStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.CompletedAt = DateTime.UtcNow;
            try { run.VaultNotePath = await _vault.WriteRunNoteAsync(task, run); } catch { }
            await db.SaveChangesAsync();
        }
    }

    private async Task UpdateProviderUsageAsync(Provider provider, BridgeUsage? usage)
    {
        if (usage is null) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var p = await db.Providers.FindAsync(provider.Id);
        if (p is null) return;

        var now = DateTime.UtcNow;
        if (p.LastUsageResetAt is null || p.LastUsageResetAt.Value.Month != now.Month || p.LastUsageResetAt.Value.Year != now.Year)
        {
            p.TokensUsedThisMonth = 0;
            p.LastUsageResetAt = now;
        }

        p.TokensUsedThisMonth += usage.InputTokens + usage.OutputTokens;
        await db.SaveChangesAsync();
    }
}