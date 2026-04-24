using System.Text;
using MissionControl.Models;

namespace MissionControl.Services;

/// <summary>
/// Thin wrapper over the Obsidian vault filesystem.
/// The vault is both the agent's knowledge base (the agent Reads files here)
/// and the destination for run transcripts (we Write files here).
/// </summary>
public class ObsidianVaultService
{
    private readonly string _vaultRoot;
    private readonly ILogger<ObsidianVaultService> _log;

    private const string RunsSubfolder = "_MissionControl/runs";

    public ObsidianVaultService(IConfiguration cfg, ILogger<ObsidianVaultService> log)
    {
        _log = log;

        var configured = cfg["Obsidian:VaultPath"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "Obsidian:VaultPath is not set. Copy appsettings.Local.json.example to " +
                "appsettings.Local.json and set Obsidian:VaultPath to your vault folder.");
        }
        _vaultRoot = Path.GetFullPath(configured);

        Directory.CreateDirectory(_vaultRoot);
        Directory.CreateDirectory(Path.Combine(_vaultRoot, RunsSubfolder));

        _log.LogInformation("Obsidian vault root: {Root}", _vaultRoot);
    }

    public string VaultRoot => _vaultRoot;

    /// <summary>
    /// Persist a run's transcript as a markdown note inside the vault.
    /// Returns the relative path (so it can be stored on AgentRun.VaultNotePath).
    /// </summary>
    public async Task<string> WriteRunNoteAsync(AgentTask task, AgentRun run, CancellationToken ct = default)
    {
        var fileName = $"{run.StartedAt:yyyy-MM-dd-HHmmss}-run-{run.Id}.md";
        var relativePath = Path.Combine(RunsSubfolder, fileName).Replace('\\', '/');
        var absolutePath = Path.Combine(_vaultRoot, relativePath);

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("type: mission-control-run");
        sb.AppendLine($"run_id: {run.Id}");
        sb.AppendLine($"task_id: {task.Id}");
        sb.AppendLine($"task_name: \"{Escape(task.Name)}\"");
        sb.AppendLine($"status: {run.Status}");
        sb.AppendLine($"started_at: {run.StartedAt:O}");
        if (run.CompletedAt is { } ca) sb.AppendLine($"completed_at: {ca:O}");
        if (!string.IsNullOrWhiteSpace(run.BridgeRunId)) sb.AppendLine($"bridge_run_id: {run.BridgeRunId}");
        sb.AppendLine("tags: [mission-control, agent-run]");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# Run #{run.Id} — {task.Name}");
        sb.AppendLine();
        sb.AppendLine("## Prompt");
        sb.AppendLine("```");
        sb.AppendLine(task.Prompt);
        sb.AppendLine("```");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(run.Result))
        {
            sb.AppendLine("## Result");
            sb.AppendLine(run.Result);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(run.ErrorMessage))
        {
            sb.AppendLine("## Error");
            sb.AppendLine("```");
            sb.AppendLine(run.ErrorMessage);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(run.Transcript))
        {
            sb.AppendLine("## Transcript");
            sb.AppendLine("```json");
            sb.AppendLine(run.Transcript);
            sb.AppendLine("```");
        }

        await File.WriteAllTextAsync(absolutePath, sb.ToString(), ct);
        _log.LogInformation("Wrote run note to vault: {Path}", relativePath);
        return relativePath;
    }

    private static string Escape(string s) => s.Replace("\"", "\\\"");
}
