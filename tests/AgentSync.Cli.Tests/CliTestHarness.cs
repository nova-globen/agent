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

    public CliResult Invoke(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var runner = new CliRunner(stdout, stderr, WorkingDirectory);
        var code = runner.Run(args);
        return new CliResult(code, stdout.ToString(), stderr.ToString());
    }

    public CliResult InvokeWithUi(AgentSync.Core.IUiLauncher launcher, params string[] args)
        => InvokeWithUi(launcher, browser: null, readiness: null, args);

    public CliResult InvokeWithUi(
        AgentSync.Core.IUiLauncher launcher,
        AgentSync.Core.IBrowserLauncher? browser,
        AgentSync.Core.IUiReadinessProbe? readiness,
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
            readiness ?? new FakeReadinessProbe());
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
