using MissionControl.Models.Enums;

namespace MissionControl.Models;

public class AgentRun
{
    public int Id { get; set; }

    public int AgentTaskId { get; set; }
    public AgentTask? AgentTask { get; set; }

    /// <summary>Set from AgentTask.ProjectId when run is created.</summary>
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    public AgentRunStatus Status { get; set; } = AgentRunStatus.Queued;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public string? Result { get; set; }

    public string? Transcript { get; set; }

    public string? ErrorMessage { get; set; }

    public string? VaultNotePath { get; set; }

    public string? BridgeRunId { get; set; }

    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? CacheReadTokens { get; set; }
    public int? CacheCreationTokens { get; set; }
    public decimal? CostUsd { get; set; }
}