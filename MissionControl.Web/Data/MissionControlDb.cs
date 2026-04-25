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
            IsEnabled = true,
            Models = new List<Model>
            {
                // GPT (Responses API)
                new() { Name = "GPT 5.5", ProviderModelId = "gpt-5.5", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5.5 Pro", ProviderModelId = "gpt-5.5-pro", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5.4", ProviderModelId = "gpt-5.4", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5.4 Pro", ProviderModelId = "gpt-5.4-pro", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5.4 Mini", ProviderModelId = "gpt-5.4-mini", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5.4 Nano", ProviderModelId = "gpt-5.4-nano", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5.3 Codex", ProviderModelId = "gpt-5.3-codex", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5.3 Codex Spark", ProviderModelId = "gpt-5.3-codex-spark", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5.2", ProviderModelId = "gpt-5.2", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5.2 Codex", ProviderModelId = "gpt-5.2-codex", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5.1", ProviderModelId = "gpt-5.1", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5.1 Codex", ProviderModelId = "gpt-5.1-codex", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5.1 Codex Max", ProviderModelId = "gpt-5.1-codex-max", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5.1 Codex Mini", ProviderModelId = "gpt-5.1-codex-mini", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5", ProviderModelId = "gpt-5", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5 Codex", ProviderModelId = "gpt-5-codex", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                new() { Name = "GPT 5 Nano", ProviderModelId = "gpt-5-nano", ApiEndpoint = "https://opencode.ai/zen/v1/responses", AiSdkPackage = "openai" },
                // Claude (Messages API)
                new() { Name = "Claude Opus 4.7", ProviderModelId = "claude-opus-4-7", ApiEndpoint = "https://opencode.ai/zen/v1/messages", AiSdkPackage = "anthropic" },
                new() { Name = "Claude Opus 4.6", ProviderModelId = "claude-opus-4-6", ApiEndpoint = "https://opencode.ai/zen/v1/messages", AiSdkPackage = "anthropic" },
                new() { Name = "Claude Opus 4.5", ProviderModelId = "claude-opus-4-5", ApiEndpoint = "https://opencode.ai/zen/v1/messages", AiSdkPackage = "anthropic" },
                new() { Name = "Claude Opus 4.1", ProviderModelId = "claude-opus-4-1", ApiEndpoint = "https://opencode.ai/zen/v1/messages", AiSdkPackage = "anthropic" },
                new() { Name = "Claude Sonnet 4.6", ProviderModelId = "claude-sonnet-4-6", ApiEndpoint = "https://opencode.ai/zen/v1/messages", AiSdkPackage = "anthropic" },
                new() { Name = "Claude Sonnet 4.5", ProviderModelId = "claude-sonnet-4-5", ApiEndpoint = "https://opencode.ai/zen/v1/messages", AiSdkPackage = "anthropic" },
                new() { Name = "Claude Sonnet 4", ProviderModelId = "claude-sonnet-4", ApiEndpoint = "https://opencode.ai/zen/v1/messages", AiSdkPackage = "anthropic" },
                new() { Name = "Claude Haiku 4.5", ProviderModelId = "claude-haiku-4-5", ApiEndpoint = "https://opencode.ai/zen/v1/messages", AiSdkPackage = "anthropic" },
                new() { Name = "Claude Haiku 3.5", ProviderModelId = "claude-3-5-haiku", ApiEndpoint = "https://opencode.ai/zen/v1/messages", AiSdkPackage = "anthropic" },
                // Gemini (Google API)
                new() { Name = "Gemini 3.1 Pro", ProviderModelId = "gemini-3.1-pro", ApiEndpoint = "https://opencode.ai/zen/v1/models/gemini-3.1-pro", AiSdkPackage = "google" },
                new() { Name = "Gemini 3 Flash", ProviderModelId = "gemini-3-flash", ApiEndpoint = "https://opencode.ai/zen/v1/models/gemini-3-flash", AiSdkPackage = "google" },
                // OpenAI-compatible (Chat Completions)
                new() { Name = "Qwen 3.6 Plus", ProviderModelId = "qwen3.6-plus", ApiEndpoint = "https://opencode.ai/zen/v1/chat/completions", AiSdkPackage = "openai-compatible" },
                new() { Name = "Qwen 3.5 Plus", ProviderModelId = "qwen3.5-plus", ApiEndpoint = "https://opencode.ai/zen/v1/chat/completions", AiSdkPackage = "openai-compatible" },
                new() { Name = "MiniMax M2.7", ProviderModelId = "minimax-m2.7", ApiEndpoint = "https://opencode.ai/zen/v1/chat/completions", AiSdkPackage = "openai-compatible" },
                new() { Name = "MiniMax M2.5", ProviderModelId = "minimax-m2.5", ApiEndpoint = "https://opencode.ai/zen/v1/chat/completions", AiSdkPackage = "openai-compatible" },
                new() { Name = "MiniMax M2.5 Free", ProviderModelId = "minimax-m2.5-free", ApiEndpoint = "https://opencode.ai/zen/v1/chat/completions", AiSdkPackage = "openai-compatible" },
                new() { Name = "GLM 5.1", ProviderModelId = "glm-5.1", ApiEndpoint = "https://opencode.ai/zen/v1/chat/completions", AiSdkPackage = "openai-compatible" },
                new() { Name = "GLM 5", ProviderModelId = "glm-5", ApiEndpoint = "https://opencode.ai/zen/v1/chat/completions", AiSdkPackage = "openai-compatible" },
                new() { Name = "Kimi K2.5", ProviderModelId = "kimi-k2.5", ApiEndpoint = "https://opencode.ai/zen/v1/chat/completions", AiSdkPackage = "openai-compatible" },
                new() { Name = "Kimi K2.6", ProviderModelId = "kimi-k2.6", ApiEndpoint = "https://opencode.ai/zen/v1/chat/completions", AiSdkPackage = "openai-compatible" },
                new() { Name = "Big Pickle", ProviderModelId = "big-pickle", ApiEndpoint = "https://opencode.ai/zen/v1/chat/completions", AiSdkPackage = "openai-compatible" },
                new() { Name = "Ling 2.6 Flash", ProviderModelId = "ling-2.6-flash", ApiEndpoint = "https://opencode.ai/zen/v1/chat/completions", AiSdkPackage = "openai-compatible" },
                new() { Name = "Hy3 Preview Free", ProviderModelId = "hy3-preview-free", ApiEndpoint = "https://opencode.ai/zen/v1/chat/completions", AiSdkPackage = "openai-compatible" },
                new() { Name = "Nemotron 3 Super Free", ProviderModelId = "nemotron-3-super-free", ApiEndpoint = "https://opencode.ai/zen/v1/chat/completions", AiSdkPackage = "openai-compatible" },
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