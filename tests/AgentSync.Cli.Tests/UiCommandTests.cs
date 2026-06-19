using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class UiCommandTests
{
    private sealed class FakeUiLauncher : IUiLauncher
    {
        private readonly string? _path;
        private readonly bool _launchSucceeds;

        public FakeUiLauncher(string? path, bool launchSucceeds = true)
        {
            _path = path;
            _launchSucceeds = launchSucceeds;
        }

        public UiLaunchRequest? Request { get; private set; }

        public string? Locate() => _path;

        public bool Launch(UiLaunchRequest request)
        {
            Request = request;
            return _launchSucceeds;
        }
    }

    [Fact]
    public void Ui_NotInstalled_ReportsLocalWebUiAndExits3()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: null);

        var result = h.InvokeWithUi(launcher, "ui");

        Assert.Equal(ExitCodes.EnvironmentProblem, result.ExitCode);
        Assert.Contains("Agent Sync UI is not installed.", result.StdOut);
        Assert.Contains("The headless CLI is working.", result.StdOut);
        Assert.Contains("local web UI", result.StdOut);
    }

    [Fact]
    public void Ui_Installed_LaunchesWithRepoPortToken()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: "/opt/agent-sync-ui");

        var result = h.InvokeWithUi(launcher, "ui");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.NotNull(launcher.Request);
        Assert.Equal("/opt/agent-sync-ui", launcher.Request!.ExecutablePath);
        Assert.Equal(h.WorkingDirectory, launcher.Request.RepoPath);
        Assert.True(launcher.Request.Port > 0);
        Assert.False(string.IsNullOrWhiteSpace(launcher.Request.Token));
    }

    [Fact]
    public void Ui_PrintsLoopbackUrlWithPortAndToken()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: "/opt/agent-sync-ui");

        var result = h.InvokeWithUi(launcher, "ui");

        var req = launcher.Request!;
        Assert.Contains($"http://127.0.0.1:{req.Port}/?token={req.Token}", result.StdOut);
        Assert.Contains("Launching Agent Sync UI", result.StdOut);
    }

    [Fact]
    public void Ui_DoesNotLeakTokenToStdErr()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: "/opt/agent-sync-ui");

        var result = h.InvokeWithUi(launcher, "ui");

        Assert.DoesNotContain(launcher.Request!.Token, result.StdErr);
    }

    [Fact]
    public void Ui_LaunchFails_Exits3()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: "/opt/agent-sync-ui", launchSucceeds: false);

        var result = h.InvokeWithUi(launcher, "ui");

        Assert.Equal(ExitCodes.EnvironmentProblem, result.ExitCode);
    }

    [Fact]
    public void Ui_OutsideGitRepo_StillLaunchesWithFolderNote()
    {
        using var h = new CliTestHarness(); // no .git
        var launcher = new FakeUiLauncher(path: "/opt/agent-sync-ui");

        var result = h.InvokeWithUi(launcher, "ui");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("not inside a Git repository", result.StdOut);
        Assert.Equal(h.WorkingDirectory, launcher.Request!.RepoPath);
    }

    [Fact]
    public void Ui_UnknownOption_InvalidUsage()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: "/opt/agent-sync-ui");

        var result = h.InvokeWithUi(launcher, "ui", "--bogus");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void Cli_DoesNotReferenceUiHostOrGuiFrameworks()
    {
        foreach (var assembly in new[] { typeof(CliRunner).Assembly, typeof(UiLauncher).Assembly })
        {
            foreach (var referenced in assembly.GetReferencedAssemblies())
            {
                var name = referenced.Name ?? string.Empty;
                Assert.DoesNotContain("maui", name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("fluentui", name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("AgentSync.Ui.Web", name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
