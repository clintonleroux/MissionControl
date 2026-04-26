using MissionControl.Models.Enums;

namespace MissionControl.Models;

public class Agent
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string SystemPrompt { get; set; } = "";

    public int ModelId { get; set; }
    public Model? Model { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}