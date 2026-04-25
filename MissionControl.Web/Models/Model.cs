namespace MissionControl.Models;

public class Model
{
    public int Id { get; set; }

    public int ProviderId { get; set; }
    public Provider? Provider { get; set; }

    public string Name { get; set; } = "";

    public string ProviderModelId { get; set; } = "";

    public string? Description { get; set; }

    public string? ApiEndpoint { get; set; }

    /// <summary>AI SDK package: "openai", "anthropic", "google", or "openai-compatible"</summary>
    public string? AiSdkPackage { get; set; }

    public int ContextWindow { get; set; } = 200000;

    public int MaxOutputTokens { get; set; } = 32000;

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<AgentTask> AgentTasks { get; set; } = new();
}