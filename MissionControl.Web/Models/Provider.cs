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

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Model> Models { get; set; } = new();
}