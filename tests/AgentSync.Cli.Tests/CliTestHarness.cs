using AgentSync.Cli;

namespace AgentSync.Cli.Tests;

/// <summary>Temporary working directory plus a helper to invoke the CLI in it.</summary>
public sealed class CliTestHarness : IDisposable
{
    public CliTestHarness()
    {
        WorkingDirectory = Path.Combine(
            Path.GetTempPath(),
            "agentsync-cli-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(WorkingDirectory);
    }

    public string WorkingDirectory { get; }

    public string MakeGitRepo()
    {
        Directory.CreateDirectory(Path.Combine(WorkingDirectory, ".git"));
        return WorkingDirectory;
    }

    public bool MakeRealGitRepo()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git")
            {
                WorkingDirectory = WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("init");
            psi.ArgumentList.Add("-q");
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null)
            {
                return false;
            }

            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public CliResult Invoke(params string[] args) => Invoke(input: null, args);

    /// <summary>
    /// Invokes the CLI with a specific stdin reader. Pass a <see cref="StringReader"/> to
    /// simulate interactive input (e.g. <c>new StringReader("y\n")</c> to answer "yes").
    /// </summary>
    public CliResult Invoke(TextReader? input, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        // Default to a non-blocking empty reader so prompts (e.g. PromptForSamples) never block.
        var stdin = input ?? new StringReader(string.Empty);
        var runner = new CliRunner(stdout, stderr, WorkingDirectory, input: stdin);
        var code = runner.Run(args);
        return new CliResult(code, stdout.ToString(), stderr.ToString());
    }

    public CliResult InvokeWithUi(AgentSync.Core.IUiLauncher launcher, params string[] args)
        => InvokeWithUi(launcher, browser: null, readiness: null, installer: null, args);

    public CliResult InvokeWithUi(
        AgentSync.Core.IUiLauncher launcher,
        AgentSync.Core.IBrowserLauncher? browser,
        AgentSync.Core.IUiReadinessProbe? readiness,
        params string[] args)
        => InvokeWithUi(launcher, browser, readiness, installer: null, args);

    public CliResult InvokeWithUi(
        AgentSync.Core.IUiLauncher launcher,
        AgentSync.Core.IUiInstaller installer,
        params string[] args)
        => InvokeWithUi(launcher, browser: null, readiness: null, installer, args);

    public CliResult InvokeWithUi(
        AgentSync.Core.IUiLauncher launcher,
        AgentSync.Core.IBrowserLauncher? browser,
        AgentSync.Core.IUiReadinessProbe? readiness,
        AgentSync.Core.IUiInstaller? installer,
        params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var runner = new CliRunner(
            stdout,
            stderr,
            WorkingDirectory,
            launcher,
            browser ?? new FakeBrowserLauncher(),
            readiness ?? new FakeReadinessProbe(),
            // A no-op installer (returns null) by default so tests never touch the network.
            installer ?? new FakeUiInstaller(),
            input: new StringReader(string.Empty));
        var code = runner.Run(args);
        return new CliResult(code, stdout.ToString(), stderr.ToString());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(WorkingDirectory))
            {
                Directory.Delete(WorkingDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}

public sealed record CliResult(int ExitCode, string StdOut, string StdErr);

/// <summary>Records the URL it was asked to open; reports configurable success.</summary>
public sealed class FakeBrowserLauncher : AgentSync.Core.IBrowserLauncher
{
    private readonly bool _succeeds;

    public FakeBrowserLauncher(bool succeeds = true) => _succeeds = succeeds;

    public string? OpenedUrl { get; private set; }

    public bool Open(string url)
    {
        OpenedUrl = url;
        return _succeeds;
    }
}

/// <summary>
/// Stand-in installer: records the version it was asked to install and returns a configurable
/// executable path (null by default, i.e. "could not install"). Never touches dotnet or the
/// network.
/// </summary>
public sealed class FakeUiInstaller : AgentSync.Core.IUiInstaller
{
    private readonly string? _result;

    public FakeUiInstaller(string? result = null) => _result = result;

    public bool Called { get; private set; }

    public string? RequestedVersion { get; private set; }

    public string? Install(string version, TextWriter log, TextWriter error)
    {
        Called = true;
        RequestedVersion = version;
        return _result;
    }
}

/// <summary>Deterministic readiness probe; reports ready/not-ready without any socket.</summary>
public sealed class FakeReadinessProbe : AgentSync.Core.IUiReadinessProbe
{
    private readonly bool _ready;

    public FakeReadinessProbe(bool ready = true) => _ready = ready;

    public int? PolledPort { get; private set; }

    public bool WaitUntilReady(int port, TimeSpan timeout)
    {
        PolledPort = port;
        return _ready;
    }
}
