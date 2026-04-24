namespace MissionControl.Models;

public enum AgentRunStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

/// <summary>
/// A single execution of an AgentTask. Captures status, timing, transcript, and the
/// path to the markdown note written into the Obsidian vault.
/// </summary>
public class AgentRun
{
    public int Id { get; set; }

    public int AgentTaskId { get; set; }
    public AgentTask? AgentTask { get; set; }

    public AgentRunStatus Status { get; set; } = AgentRunStatus.Queued;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>Final assistant text output (short).</summary>
    public string? Result { get; set; }

    /// <summary>Full streamed transcript (JSONL or plain text, bounded).</summary>
    public string? Transcript { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Relative path (from vault root) to the markdown note written for this run.</summary>
    public string? VaultNotePath { get; set; }

    /// <summary>The bridge's run id, useful for cross-referencing logs.</summary>
    public string? BridgeRunId { get; set; }
}
