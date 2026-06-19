using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
/// Installs the external <c>agent-sync-ui</c> on demand when <c>agent ui</c> can't find it,
/// so the first run is self-service instead of a manual download. Two strategies: install
/// the <c>AgentSync.Ui</c> .NET tool when a <c>dotnet</c> SDK is on PATH, otherwise download
/// and extract the self-contained release archive for the current platform.
/// </summary>
public interface IUiInstaller
{
    /// <summary>
    /// Attempts to install the UI for the given Agent Sync <paramref name="version"/>,
    /// writing human-readable progress to <paramref name="log"/> and problems to
    /// <paramref name="error"/>. Returns the path to the installed executable, or
    /// <c>null</c> if installation was not possible.
    /// </summary>
    string? Install(string version, TextWriter log, TextWriter error);
}

/// <summary>
/// Default <see cref="IUiInstaller"/>. Prefers <c>dotnet tool install --global AgentSync.Ui</c>
/// (matching the CLI's version) when a <c>dotnet</c> command is available; otherwise downloads
/// the <c>agent-sync-ui-v&lt;version&gt;-&lt;rid&gt;</c> archive from GitHub Releases and
/// extracts it into a per-version cache under the user profile. Never throws — failure is
/// reported by returning <c>null</c> so the caller can print install guidance.
/// </summary>
public sealed class UiInstaller : IUiInstaller
{
    public const string PackageId = "AgentSync.Ui";
    public const string ReleaseBaseUrl = "https://github.com/nova-globen/agent/releases/download";

    private readonly IUiLauncher _launcher;

    public UiInstaller(IUiLauncher? launcher = null) => _launcher = launcher ?? new UiLauncher();

    public string? Install(string version, TextWriter log, TextWriter error)
    {
        if (string.IsNullOrWhiteSpace(version) || version is "0.0.0")
        {
            // A local dev build has no published tool/release to install from.
            error.WriteLine("note: cannot auto-install the UI for an unreleased build.");
            return null;
        }

        if (DotnetAvailable())
        {
            log.WriteLine($"Installing the Agent Sync UI as a .NET tool ({PackageId} {version})...");
            if (InstallDotnetTool(version, log, error))
            {
                var installed = _launcher.Locate() ?? GlobalToolExecutable();
                if (installed is not null && File.Exists(installed))
                {
                    return installed;
                }
            }

            error.WriteLine("note: the .NET tool install did not yield a usable agent-sync-ui; trying a direct download.");
        }

        return DownloadAndExtract(version, log, error);
    }

    /// <summary>Maps the current OS/architecture to a published release runtime identifier.</summary>
    public static string ResolveRid()
    {
        if (OperatingSystem.IsWindows())
        {
            return "win-x64";
        }

        var isArm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        if (OperatingSystem.IsMacOS())
        {
            return isArm64 ? "osx-arm64" : "osx-x64";
        }

        return isArm64 ? "linux-arm64" : "linux-x64";
    }

    /// <summary>The GitHub Releases download URL for the UI archive of a version + RID.</summary>
    public static string DownloadUrl(string version, string rid)
    {
        var ext = rid.StartsWith("win", StringComparison.Ordinal) ? "zip" : "tar.gz";
        return $"{ReleaseBaseUrl}/v{version}/agent-sync-ui-v{version}-{rid}.{ext}";
    }

    /// <summary>The per-version cache directory the downloaded UI is extracted into.</summary>
    public static string CacheDirectory(string version, string rid)
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agent-sync", "ui", $"v{version}", rid);

    private static string ExecutableInDir(string dir)
        => Path.Combine(dir, OperatingSystem.IsWindows() ? UiLauncher.ExecutableName + ".exe" : UiLauncher.ExecutableName);

    private static bool DotnetAvailable()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var names = OperatingSystem.IsWindows() ? new[] { "dotnet.exe", "dotnet" } : new[] { "dotnet" };
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in names)
            {
                try
                {
                    if (File.Exists(Path.Combine(dir.Trim(), name)))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore malformed PATH entries.
                }
            }
        }

        return false;
    }

    private static bool InstallDotnetTool(string version, TextWriter log, TextWriter error)
    {
        // `install` succeeds when the tool is absent; if it's already present that fails, so
        // fall back to `update` (which also pins the requested version).
        if (RunDotnet(new[] { "tool", "install", "--global", PackageId, "--version", version }, log, error))
        {
            return true;
        }

        return RunDotnet(new[] { "tool", "update", "--global", PackageId, "--version", version }, log, error);
    }

    private static bool RunDotnet(string[] args, TextWriter log, TextWriter error)
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }

            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    error.WriteLine(stderr.TrimEnd());
                }

                return false;
            }

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                log.WriteLine(stdout.TrimEnd());
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GlobalToolExecutable()
        => ExecutableInDir(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools"));

    private string? DownloadAndExtract(string version, TextWriter log, TextWriter error)
    {
        var rid = ResolveRid();
        var dir = CacheDirectory(version, rid);
        var exe = ExecutableInDir(dir);

        // Reuse a previously extracted copy rather than downloading again.
        if (File.Exists(exe))
        {
            return exe;
        }

        var url = DownloadUrl(version, rid);
        log.WriteLine($"Downloading the Agent Sync UI from {url} ...");

        var tempArchive = Path.Combine(Path.GetTempPath(), $"agent-sync-ui-{Guid.NewGuid():N}");
        try
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                using var response = client.GetAsync(url).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    error.WriteLine($"error: download failed (HTTP {(int)response.StatusCode}). No release asset for v{version} / {rid}?");
                    return null;
                }

                using var src = response.Content.ReadAsStream();
                using var dest = File.Create(tempArchive);
                src.CopyTo(dest);
            }

            Directory.CreateDirectory(dir);
            if (url.EndsWith(".zip", StringComparison.Ordinal))
            {
                ZipFile.ExtractToDirectory(tempArchive, dir, overwriteFiles: true);
            }
            else
            {
                using var fs = File.OpenRead(tempArchive);
                using var gz = new GZipStream(fs, CompressionMode.Decompress);
                TarFile.ExtractToDirectory(gz, dir, overwriteFiles: true);
            }

            if (!File.Exists(exe))
            {
                error.WriteLine("error: the downloaded archive did not contain agent-sync-ui.");
                return null;
            }

            if (!OperatingSystem.IsWindows())
            {
                var mode = File.GetUnixFileMode(exe);
                File.SetUnixFileMode(exe, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            }

            log.WriteLine($"Installed the Agent Sync UI to {dir}");
            return exe;
        }
        catch (Exception ex)
        {
            error.WriteLine($"error: could not download or extract the Agent Sync UI: {ex.Message}");
            return null;
        }
        finally
        {
            try { File.Delete(tempArchive); } catch { /* best effort */ }
        }
    }
}

/// <summary>
/// Helpers for a local UI session: a free loopback port, a per-launch session token,
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

    /// <summary>Generates a cryptographically random, per-launch session token.</summary>
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
