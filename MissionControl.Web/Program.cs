using Microsoft.EntityFrameworkCore;
using MissionControl.Components;
using MissionControl.Data;
using MissionControl.Services;

var builder = WebApplication.CreateBuilder(args);

// Load appsettings.Local.json (gitignored) on top of appsettings.json so secrets like
// ApiKey and host-specific paths (vault) stay out of version control.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// EF Core + SQLite. Use a factory so services that hop threads (AgentRunner background work)
// each get their own scoped context.
var connectionString = builder.Configuration.GetConnectionString("MissionControl")
                       ?? "Data Source=missioncontrol.db";
builder.Services.AddDbContextFactory<MissionControlDb>(opt => opt.UseSqlite(connectionString));

// Claude bridge (Node.js sidecar) as a typed HttpClient
builder.Services.AddHttpClient<ClaudeBridgeClient>(client =>
{
    var baseUrl = builder.Configuration["ClaudeBridge:BaseUrl"] ?? "http://localhost:4100";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromMinutes(10); // agent runs can be long
});

builder.Services.AddSingleton<ObsidianVaultService>();
builder.Services.AddScoped<AgentRunner>();

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

// Create & seed the DB on startup.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MissionControlDb>>();
    using var db = factory.CreateDbContext();
    MissionControlDb.EnsureSeeded(db);
}

app.Run();
