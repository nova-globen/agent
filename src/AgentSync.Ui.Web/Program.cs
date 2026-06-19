using AgentSync.Ui.Abstractions;
using AgentSync.Ui.Web;
using AgentSync.Ui.Web.Components;
using Microsoft.FluentUI.AspNetCore.Components;

if (!WebOptions.TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine($"error: {error}");
    Console.Error.WriteLine(WebOptions.Usage);
    return 2;
}

var builder = WebApplication.CreateBuilder(args);

// Bind to loopback only on the chosen port. Never bind 0.0.0.0 (no remote exposure).
builder.WebHost.UseUrls($"http://127.0.0.1:{options!.Port}");

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddSingleton(options);
// One application service scoped to the single repository this session manages.
builder.Services.AddSingleton(new AgentSyncApp(options.Repo));

var app = builder.Build();

// Session-token gate: every request must present the token (cookie first, then the
// initial ?token= query). The only exception is the unauthenticated readiness endpoint.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "/";
    if (SessionGate.IsPublicPath(path))
    {
        await next();
        return;
    }

    var cookieToken = context.Request.Cookies.TryGetValue(SessionGate.CookieName, out var c) ? c : null;
    var queryToken = context.Request.Query.TryGetValue(SessionGate.QueryName, out var q) ? q.ToString() : null;

    switch (SessionGate.Decide(options.Token, cookieToken, queryToken))
    {
        case SessionDecision.AllowFromCookie:
            await next();
            return;

        case SessionDecision.AllowFromQuery:
            // Exchange the token into an HttpOnly cookie, then redirect to the same path
            // with the token stripped from the URL so it never lingers in history.
            context.Response.Cookies.Append(SessionGate.CookieName, options.Token, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = false, // loopback http only
                IsEssential = true,
            });
            var queryPairs = context.Request.Query
                .Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value.ToString()));
            context.Response.Redirect(SessionGate.RedirectWithoutToken(path, queryPairs));
            return;

        default:
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: missing or invalid session token.");
            return;
    }
});

// Unauthenticated readiness probe: returns only "ok", never any repository data. The
// CLI's `agent ui` polls this to confirm the host started before opening the browser.
app.MapGet("/healthz", () => Results.Text("ok", "text/plain"));

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
return 0;

/// <summary>Exposed so integration tests can host the app via WebApplicationFactory.</summary>
public partial class Program;
