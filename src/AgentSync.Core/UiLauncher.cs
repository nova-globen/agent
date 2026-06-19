using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace AgentSync.Core;

/// <summary>A request to launch the local web UI: which executable, repo, port, and session token.</summary>
public sealed record UiLaunchRequest(string ExecutablePath, string RepoPath, int Port, string Token);

/// <summary>
/// Locates and launches the external Agent Sync GUI executable. The GUI is a separate,
/// optional product surface — a localhost Blazor web host. The CLI discovers and starts
/// it by process and must never reference the UI project at compile time.
/// </summary>
public interface IUiLauncher
{
    /// <summary>Returns the path to an installed GUI executable, or <c>null</c> if none is found.</summary>
    string? Locate();

    /// <summary>Starts the GUI executable. Returns false on failure.</summary>
    bool Launch(UiLaunchRequest request);
}

/// <summary>
/// Default <see cref="IUiLauncher"/>: discovers <c>agent-sync-ui</c> via the
/// <c>AGENT_SYNC_UI</c> override, then next to the running binary, then on <c>PATH</c>,
/// and launches it with <c>--repo &lt;path&gt; --port &lt;port&gt; --token &lt;token&gt;</c>.
/// The launched host is the localhost Blazor web UI; the CLI knows only the executable
/// name and the launch protocol.
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

    public bool Launch(UiLaunchRequest request)
    {
        try
        {
            var psi = new ProcessStartInfo(request.ExecutablePath) { UseShellExecute = false };
            psi.ArgumentList.Add("--repo");
            psi.ArgumentList.Add(request.RepoPath);
            psi.ArgumentList.Add("--port");
            psi.ArgumentList.Add(request.Port.ToString());
            psi.ArgumentList.Add("--token");
            psi.ArgumentList.Add(request.Token);
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

/// <summary>
/// Helpers for a local UI session: a free loopback port, a short-lived session token,
/// and the access URL. The token gates local browser access to the web UI.
/// </summary>
public static class UiSession
{
    /// <summary>Finds a free TCP port on the loopback interface.</summary>
    public static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Generates a cryptographically random, short-lived session token.</summary>
    public static string NewToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    /// <summary>Builds the loopback access URL the browser should open.</summary>
    public static string Url(int port, string token)
        => $"http://127.0.0.1:{port}/?token={token}";
}
