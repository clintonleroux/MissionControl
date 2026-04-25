namespace MissionControl.Models;

public enum ProviderType
{
    Opencode,
    Claude
}

public class Provider
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public ProviderType Type { get; set; }

    public string ApiKey { get; set; } = "";

    /// <summary>Bridge endpoint URL (e.g. http://localhost:4100). Used for routing.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>AI API endpoint URL (e.g. https://api.deepseek.com). Sent to bridge.</summary>
    public string? ApiEndpoint { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int? MonthlyCreditLimit { get; set; }

    public int TokensUsedThisMonth { get; set; }

    public DateTime? LastUsageResetAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Model> Models { get; set; } = new();
}