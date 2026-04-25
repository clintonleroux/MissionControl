using Microsoft.EntityFrameworkCore;
using MissionControl.Models;

namespace MissionControl.Data;

public class MissionControlDb : DbContext
{
    public MissionControlDb(DbContextOptions<MissionControlDb> options) : base(options) { }

    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Model> Models => Set<Model>();
    public DbSet<AgentTask> AgentTasks => Set<AgentTask>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Provider>()
            .HasMany(p => p.Models)
            .WithOne(m => m.Provider)
            .HasForeignKey(m => m.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Model>()
            .HasMany(m => m.AgentTasks)
            .WithOne(t => t.Model)
            .HasForeignKey(t => t.ModelId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<AgentTask>()
            .HasMany(t => t.Runs)
            .WithOne(r => r.AgentTask)
            .HasForeignKey(r => r.AgentTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<AgentRun>()
            .Property(r => r.Status)
            .HasConversion<string>();
    }

    public static async Task EnsureSeededAsync(MissionControlDb db)
    {
        try { await db.Database.EnsureCreatedAsync(); }
        catch (Exception ex) { Console.WriteLine($"DB creation warning: {ex.Message}"); }

        if (await db.Providers.AnyAsync())
            return;

        var claude = new Provider
        {
            Name = "Claude",
            Type = ProviderType.Claude,
            BaseUrl = "http://localhost:4200",
            IsEnabled = true,
            MonthlyCreditLimit = 500,
            Models = new List<Model>
            {
                new() { Name = "Sonnet 4", ProviderModelId = "claude-sonnet-4-6", ContextWindow = 200000, MaxOutputTokens = 32000 },
                new() { Name = "Haiku 4", ProviderModelId = "claude-haiku-4-5-20251001", ContextWindow = 200000, MaxOutputTokens = 32000 }
            }
        };

        var opencode = new Provider
        {
            Name = "OpenCode.ai",
            Type = ProviderType.Opencode,
            BaseUrl = "http://localhost:4100",
            ApiEndpoint = "https://opencode.ai/zen/v1",
            IsEnabled = true,
            MonthlyCreditLimit = 500,
            Models = new List<Model>
            {
                new() { Name = "Qwen 3.6+", ProviderModelId = "opencode/qwen3.6-plus", ContextWindow = 200000, MaxOutputTokens = 32000 },
                new() { Name = "Kimi K2.6", ProviderModelId = "opencode/kimi-k2.6", ContextWindow = 200000, MaxOutputTokens = 32000 },
                new() { Name = "MiniMax M2.7", ProviderModelId = "opencode/minimax-m2.7", ContextWindow = 200000, MaxOutputTokens = 32000 },
                new() { Name = "GLM 5.1", ProviderModelId = "opencode/glm-5.1", ContextWindow = 200000, MaxOutputTokens = 32000 }
            }
        };

        db.Providers.AddRange(claude, opencode);
        await db.SaveChangesAsync();

        var defaultModel = db.Models.First(m => m.ProviderModelId == "claude-sonnet-4-6");

        db.AgentTasks.AddRange(
            new AgentTask
            {
                Name = "Summarize today's notes",
                Prompt = "Read every markdown note in the vault whose filename contains today's date (YYYY-MM-DD). Produce a 5-bullet summary. Write it to _MissionControl/daily-summary.md.",
                SystemPrompt = "You are a diligent note-keeper. Be concise.",
                AllowedTools = "Read,Write,Grep,Glob",
                MaxTurns = 8,
                ModelId = defaultModel.Id
            },
            new AgentTask
            {
                Name = "Tag untagged notes",
                Prompt = "Find markdown notes in the vault that have no YAML frontmatter 'tags' field. For each one, read the content and add 3 relevant tags via frontmatter. Report how many notes you updated.",
                AllowedTools = "Read,Edit,Grep,Glob",
                MaxTurns = 15,
                ModelId = defaultModel.Id
            }
        );
        await db.SaveChangesAsync();
    }
}