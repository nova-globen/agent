using System.Diagnostics;
using System.Net;
using System.Net.Http;
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

    /// <summary>Builds the loopback access URL the browser should open (carries the token).</summary>
    public static string Url(int port, string token)
        => $"http://127.0.0.1:{port}/?token={token}";

    /// <summary>The loopback base URL without the token (safe to print after the browser opens).</summary>
    public static string BaseUrl(int port)
        => $"http://127.0.0.1:{port}/";

    /// <summary>The loopback readiness URL the launcher polls before opening the browser.</summary>
    public static string HealthUrl(int port)
        => $"http://127.0.0.1:{port}/healthz";
}

/// <summary>
/// Opens a URL in the user's default browser. Abstracted so <c>agent ui</c> can be tested
/// without actually launching a browser.
/// </summary>
public interface IBrowserLauncher
{
    /// <summary>Attempts to open <paramref name="url"/>. Returns false if the browser could not be launched.</summary>
    bool Open(string url);
}

/// <summary>
/// Default <see cref="IBrowserLauncher"/>: opens the URL using the platform's standard
/// mechanism (shell-execute on Windows, <c>open</c> on macOS, <c>xdg-open</c> on Linux).
/// Never throws — failure is reported by returning false so the caller can print the URL.
/// </summary>
public sealed class BrowserLauncher : IBrowserLauncher
{
    public bool Open(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var p = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return p is not null;
            }

            var exe = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
            psi.ArgumentList.Add(url);
            using var process = Process.Start(psi);
            return process is not null;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Polls the local web UI's readiness endpoint until it responds or a timeout elapses, so
/// <c>agent ui</c> can confirm the host actually started before opening the browser.
/// Abstracted to keep the launcher deterministic in tests (no real process/socket needed).
/// </summary>
public interface IUiReadinessProbe
{
    /// <summary>Returns true once the host on <paramref name="port"/> is ready, or false on timeout.</summary>
    bool WaitUntilReady(int port, TimeSpan timeout);
}

/// <summary>
/// Default <see cref="IUiReadinessProbe"/>: repeatedly GETs <c>/healthz</c> on the loopback
/// port until it returns success or the timeout elapses. The endpoint is unauthenticated
/// and returns only a minimal "ok" (no repository data).
/// </summary>
public sealed class HttpUiReadinessProbe : IUiReadinessProbe
{
    public bool WaitUntilReady(int port, TimeSpan timeout)
    {
        var url = UiSession.HealthUrl(port);
        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = client.GetAsync(url).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
                // Host not up yet; keep polling until the deadline.
            }

            Thread.Sleep(100);
        }

        return false;
    }
}
