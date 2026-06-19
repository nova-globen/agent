using AgentSync.Ui.Abstractions;
using Microsoft.Extensions.Logging;

namespace AgentSync.Ui.Maui;

/// <summary>
/// MAUI Blazor Hybrid host entry. Registers the UI-independent application service
/// (<see cref="AgentSyncApp"/>) so Razor components depend on it rather than reaching
/// into <c>AgentSync.Core</c> or doing repository mutation themselves.
/// </summary>
/// <remarks>
/// Skeleton: requires the MAUI workload to build (see the csproj header). The actual
/// component tree and DI wiring land in Milestone H (GUI MVP).
/// </remarks>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        // The repository to open is passed by `agent ui --repo <path>`; default to cwd.
        var repoPath = ResolveRepoArg() ?? Directory.GetCurrentDirectory();
        builder.Services.AddSingleton(_ => new AgentSyncApp(repoPath));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static string? ResolveRepoArg()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--repo")
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
