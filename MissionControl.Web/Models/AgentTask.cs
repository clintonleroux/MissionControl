namespace MissionControl.Models;

/// <summary>
/// A reusable unit of work an agent can be asked to perform.
/// Think of it as a saved prompt + config.
/// </summary>
public class AgentTask
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Instruction given to the agent on each run.</summary>
    public string Prompt { get; set; } = "";

    /// <summary>Optional system prompt override.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>Comma-separated tool allowlist (e.g. "Read,Write,Grep"). Null = default allowlist.</summary>
    public string? AllowedTools { get; set; }

    /// <summary>Max agent turns before the runner stops.</summary>
    public int MaxTurns { get; set; } = 10;

    /// <summary>Optional model to use. If null, provider's default is used.</summary>
    public int ModelId { get; set; }
    public Model? Model { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<AgentRun> Runs { get; set; } = new();
}
