using Microsoft.EntityFrameworkCore;
using MissionControl.Data;
using MissionControl.Models;

namespace MissionControl.Services;

/// <summary>
/// Orchestrates a task execution: creates the AgentRun row, calls the Claude bridge,
/// writes the transcript back to the vault, and updates the row with the outcome.
/// </summary>
public class AgentRunner
{
    private readonly IDbContextFactory<MissionControlDb> _dbFactory;
    private readonly ClaudeBridgeClient _bridge;
    private readonly ObsidianVaultService _vault;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AgentRunner> _log;

    public AgentRunner(
        IDbContextFactory<MissionControlDb> dbFactory,
        ClaudeBridgeClient bridge,
        ObsidianVaultService vault,
        IConfiguration cfg,
        ILogger<AgentRunner> log)
    {
        _dbFactory = dbFactory;
        _bridge = bridge;
        _vault = vault;
        _cfg = cfg;
        _log = log;
    }

    /// <summary>
    /// Fire-and-forget dispatch of a run. Caller gets the runId immediately;
    /// the UI polls / re-renders as the row updates.
    /// </summary>
    public async Task<int> StartAsync(int taskId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var task = await db.AgentTasks.FindAsync(new object[] { taskId }, ct)
                   ?? throw new InvalidOperationException($"Task {taskId} not found.");

        var run = new AgentRun
        {
            AgentTaskId = task.Id,
            Status = AgentRunStatus.Queued,
            StartedAt = DateTime.UtcNow
        };
        db.AgentRuns.Add(run);
        await db.SaveChangesAsync(ct);

        // Background execution — don't block the UI thread on a multi-minute agent run.
        _ = Task.Run(() => ExecuteAsync(run.Id), CancellationToken.None);
        return run.Id;
    }

    private async Task ExecuteAsync(int runId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var run = await db.AgentRuns.Include(r => r.AgentTask).FirstOrDefaultAsync(r => r.Id == runId);
        if (run?.AgentTask is null)
        {
            _log.LogError("Run {RunId} or its task vanished before execution.", runId);
            return;
        }

        var task = run.AgentTask;
        run.Status = AgentRunStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        try
        {
            var apiKey = _cfg["Anthropic:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Anthropic:ApiKey is not set. Copy appsettings.Local.json.example to " +
                    "appsettings.Local.json and set Anthropic:ApiKey.");
            }

            var allowedTools = string.IsNullOrWhiteSpace(task.AllowedTools)
                ? null
                : task.AllowedTools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var req = new BridgeRunRequest(
                TaskName: task.Name,
                Prompt: task.Prompt,
                SystemPrompt: task.SystemPrompt,
                Cwd: _vault.VaultRoot, // agent's Read/Write/Grep operate against the vault
                AllowedTools: allowedTools,
                MaxTurns: task.MaxTurns,
                ApiKey: apiKey
            );

            var result = await _bridge.RunAsync(req);

            run.BridgeRunId = result.RunId;
            run.Result = result.Result;
            run.Transcript = result.Transcript;
            run.Status = result.Status == "success" ? AgentRunStatus.Succeeded : AgentRunStatus.Failed;
            run.ErrorMessage = result.Error;
            run.CompletedAt = DateTime.UtcNow;

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
            try { run.VaultNotePath = await _vault.WriteRunNoteAsync(task, run); } catch { /* best effort */ }
            await db.SaveChangesAsync();
        }
    }
}
