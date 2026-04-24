using Microsoft.EntityFrameworkCore;
using MissionControl.Models;

namespace MissionControl.Data;

public class MissionControlDb : DbContext
{
    public MissionControlDb(DbContextOptions<MissionControlDb> options) : base(options) { }

    public DbSet<AgentTask> AgentTasks => Set<AgentTask>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AgentTask>()
            .HasMany(t => t.Runs)
            .WithOne(r => r.AgentTask)
            .HasForeignKey(r => r.AgentTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Store enum as string so the DB is readable.
        b.Entity<AgentRun>()
            .Property(r => r.Status)
            .HasConversion<string>();
    }

    /// <summary>Seed a couple of example tasks on first run so the UI isn't empty.</summary>
    public static void EnsureSeeded(MissionControlDb db)
    {
        db.Database.EnsureCreated();
        if (db.AgentTasks.Any()) return;

        db.AgentTasks.AddRange(
            new AgentTask
            {
                Name = "Summarize today's notes",
                Prompt = "Read every markdown note in the vault whose filename contains today's date (YYYY-MM-DD). Produce a 5-bullet summary. Write it to _MissionControl/daily-summary.md.",
                SystemPrompt = "You are a diligent note-keeper. Be concise.",
                AllowedTools = "Read,Write,Grep,Glob",
                MaxTurns = 8
            },
            new AgentTask
            {
                Name = "Tag untagged notes",
                Prompt = "Find markdown notes in the vault that have no YAML frontmatter 'tags' field. For each one, read the content and add 3 relevant tags via frontmatter. Report how many notes you updated.",
                AllowedTools = "Read,Edit,Grep,Glob",
                MaxTurns = 15
            }
        );
        db.SaveChanges();
    }
}
