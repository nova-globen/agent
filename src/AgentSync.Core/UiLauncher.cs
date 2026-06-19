using System.Diagnostics;

namespace AgentSync.Core;

/// <summary>
/// Locates and launches the external Agent Sync GUI executable. The GUI is a separate,
/// optional product surface: the CLI discovers and starts it by process, and must never
/// reference the GUI (MAUI/OpenMaui) projects at compile time.
/// </summary>
public interface IUiLauncher
{
    /// <summary>Returns the path to an installed GUI executable, or <c>null</c> if none is found.</summary>
    string? Locate();

    /// <summary>Starts the GUI executable for <paramref name="repoPath"/>. Returns false on failure.</summary>
    bool Launch(string executablePath, string repoPath);
}

/// <summary>
/// Default <see cref="IUiLauncher"/>: discovers <c>agent-sync-ui</c> via the
/// <c>AGENT_SYNC_UI</c> override, then next to the running binary, then on <c>PATH</c>,
/// and launches it with <c>--repo &lt;path&gt;</c>.
/// </summary>
public sealed class UiLauncher : IUiLauncher
{
    public const string ExecutableName = "agent-sync-ui";
    public const string OverrideEnvVar = "AGENT_SYNC_UI";

    public string? Locate()
    {
        var overridePath = Environment.GetEnvironmentVariable(OverrideEnvVar);
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        foreach (var name in CandidateNames())
        {
            var beside = Path.Combine(AppContext.BaseDirectory, name);
            if (File.Exists(beside))
            {
                return beside;
            }
        }

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in CandidateNames())
            {
                try
                {
                    var candidate = Path.Combine(dir.Trim(), name);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // Ignore malformed PATH entries.
                }
            }
        }

        return null;
    }

    public bool Launch(string executablePath, string repoPath)
    {
        try
        {
            var psi = new ProcessStartInfo(executablePath) { UseShellExecute = false };
            psi.ArgumentList.Add("--repo");
            psi.ArgumentList.Add(repoPath);
            using var process = Process.Start(psi);
            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> CandidateNames()
        => OperatingSystem.IsWindows()
            ? new[] { ExecutableName + ".exe", ExecutableName }
            : new[] { ExecutableName };
}
