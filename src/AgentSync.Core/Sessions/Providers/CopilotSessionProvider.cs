namespace AgentSync.Core.Sessions.Providers;

/// <summary>
/// GitHub Copilot CLI keeps session/history state under <c>~/.copilot/</c> (history,
/// session-state, and logs). The per-project association is not a simple path-derived folder
/// name, so sessions are matched by the project directory appearing inside their contents.
/// Experimental: Copilot's on-disk layout is less stable than Claude's or Codex's.
/// </summary>
public sealed class CopilotSessionProvider : ContentMatchSessionProvider
{
    public override string Id => "copilot";
    public override string DisplayName => "GitHub Copilot CLI";
    public override IReadOnlyList<string> Aliases => new[] { "github-copilot", "gh-copilot" };

    public override string Root(SessionEnvironment env)
    {
        var overrideHome = Environment.GetEnvironmentVariable("COPILOT_HOME");
        return string.IsNullOrEmpty(overrideHome)
            ? Path.Combine(env.HomeDirectory, ".copilot")
            : overrideHome;
    }

    protected override IReadOnlyList<string> ScanSubdirectories => new[]
    {
        "session-state",
        "history-session-state",
        "history",
        "sessions",
    };
}
