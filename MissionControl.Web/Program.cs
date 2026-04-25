using Microsoft.EntityFrameworkCore;
using MissionControl.Components;
using MissionControl.Data;
using MissionControl.Models;
using MissionControl.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("MissionControl")
                       ?? "Data Source=missioncontrol.db";
builder.Services.AddDbContextFactory<MissionControlDb>(opt => opt.UseSqlite(connectionString));

builder.Services.AddSingleton<ObsidianVaultService>();
builder.Services.AddSingleton<IProviderBridgeRegistry, ProviderBridgeRegistry>();
builder.Services.AddSingleton<AgentRunner>();
builder.Services.AddHostedService<TaskSchedulerService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MissionControlDb>>();
    using var db = factory.CreateDbContext();
    await MissionControlDb.EnsureSeededAsync(db);

    var registry = scope.ServiceProvider.GetRequiredService<IProviderBridgeRegistry>();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

    var providers = db.Providers.Include(p => p.Models).Where(p => p.IsEnabled).ToList();
    foreach (var provider in providers)
    {
        try
        {
            var baseUrl = provider.BaseUrl ?? GetDefaultUrl(provider.Type);
            var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromMinutes(10) };
            var logger = loggerFactory.CreateLogger<ProviderBridge>();
            var bridge = new ProviderBridge(http, logger, provider.Type.ToString().ToLowerInvariant());
            registry.RegisterBridge(provider.Type, bridge);
            Console.WriteLine($"Registered bridge for {provider.Name} at {baseUrl}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register bridge for {provider.Name}: {ex.Message}");
        }
    }
}

app.Run();

static string GetDefaultUrl(ProviderType type) => type switch
{
    ProviderType.Opencode => "http://localhost:4100",
    ProviderType.Claude => "http://localhost:4200",
    _ => "http://localhost:4100"
};