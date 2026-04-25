using Microsoft.EntityFrameworkCore;
using MissionControl.Data;
using MissionControl.Models;
using MissionControl.Models.Enums;

namespace MissionControl.Services;

public class TaskSchedulerService : BackgroundService
{
    private readonly IDbContextFactory<MissionControlDb> _dbFactory;
    private readonly AgentRunner _runner;
    private readonly ILogger<TaskSchedulerService> _log;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(10);

    public TaskSchedulerService(IDbContextFactory<MissionControlDb> dbFactory, AgentRunner runner, ILogger<TaskSchedulerService> log)
    {
        _dbFactory = dbFactory;
        _runner = runner;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingTasksAsync(ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error processing pending tasks.");
            }
            await Task.Delay(_interval, ct);
        }
    }

    private async Task ProcessPendingTasksAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var runningTaskIds = await db.AgentTasks
            .Where(t => t.Status == MissionControl.Models.Enums.TaskStatus.Running)
            .Select(t => t.ProjectId)
            .ToListAsync(ct);

        var pendingTasks = await db.AgentTasks
            .Where(t => t.Status == MissionControl.Models.Enums.TaskStatus.Pending)
            .Where(t => !runningTaskIds.Contains(t.ProjectId))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

        foreach (var task in pendingTasks)
        {
            try
            {
                _log.LogInformation("Auto-running pending task {TaskId}: {TaskName} (priority {Priority})", task.Id, task.Name, task.Priority);
                await _runner.StartAsync(task.Id, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to start task {TaskId}.", task.Id);
            }
        }
    }
}