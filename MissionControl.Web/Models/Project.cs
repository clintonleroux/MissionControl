namespace MissionControl.Models;

public class Project
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public string FolderPath { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<AgentTask> Tasks { get; set; } = new();

    public List<ProjectAgent> ProjectAgents { get; set; } = new();
}