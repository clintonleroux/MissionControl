using MissionControl.Models.Enums;

namespace MissionControl.Models;

public class ProjectAgent
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public TaskStage Stage { get; set; }

    public int AgentId { get; set; }
    public Agent? Agent { get; set; }
}