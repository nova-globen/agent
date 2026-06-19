using AgentSync.Ui.Abstractions;
using AgentSync.Ui.Web;
using AgentSync.Ui.Web.Components;
using Microsoft.FluentUI.AspNetCore.Components;

var options = WebOptions.Parse(args);

var builder = WebApplication.CreateBuilder(args);

// Bind to loopback only on the chosen port. Never bind 0.0.0.0 (no remote exposure).
builder.WebHost.UseUrls($"http://127.0.0.1:{options.Port}");

builder.Services.AddRazorComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddSingleton(options);
// One application service scoped to the single repository this session manages.
builder.Services.AddSingleton(new AgentSyncApp(options.Repo));

var app = builder.Build();

// Session-token gate: every request must present the token (query first, then cookie).
app.Use(async (context, next) =>
{
    if (!SessionGate.Authorize(context, options.Token))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Unauthorized: missing or invalid session token.");
        return;
    }

    await next();
});

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>();

app.Run();
