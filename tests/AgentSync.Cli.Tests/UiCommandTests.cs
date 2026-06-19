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
    public void Ui_NotInstalled_AndInstallFails_ReportsGuidanceAndExits3()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: null);
        var installer = new FakeUiInstaller(result: null); // install not possible

        var result = h.InvokeWithUi(launcher, installer, "ui");

        Assert.Equal(ExitCodes.EnvironmentProblem, result.ExitCode);
        Assert.True(installer.Called);
        Assert.Contains("Agent Sync UI is not installed.", result.StdOut);
        Assert.Contains("The headless CLI is working.", result.StdOut);
        Assert.Contains("local web UI", result.StdOut);
        // Falls back to actionable guidance: the .NET tool and the release download.
        Assert.Contains("dotnet tool install --global AgentSync.Ui", result.StdOut);
        Assert.Contains("github.com/nova-globen/agent/releases", result.StdOut);
    }

    [Fact]
    public void Ui_NotInstalled_AutoInstalls_ThenLaunches()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: null); // not found on disk...
        var installer = new FakeUiInstaller(result: "/installed/agent-sync-ui"); // ...so install it

        var result = h.InvokeWithUi(launcher, installer, "ui");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(installer.Called);
        Assert.Contains("setting it up now", result.StdOut);
        Assert.NotNull(launcher.Request);
        Assert.Equal("/installed/agent-sync-ui", launcher.Request!.ExecutablePath);
    }

    [Fact]
    public void Ui_AlreadyInstalled_DoesNotAttemptInstall()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: "/opt/agent-sync-ui");
        var installer = new FakeUiInstaller(result: "/should/not/be/used");

        var result = h.InvokeWithUi(launcher, installer, "ui");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.False(installer.Called);
        Assert.Equal("/opt/agent-sync-ui", launcher.Request!.ExecutablePath);
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
    public void Ui_OpensBrowserAtTokenUrl_AndPrintsCleanUrl()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: "/opt/agent-sync-ui");
        var browser = new FakeBrowserLauncher(succeeds: true);

        var result = h.InvokeWithUi(launcher, browser, readiness: null, "ui");

        var req = launcher.Request!;
        // The browser is opened with the token URL...
        Assert.Equal($"http://127.0.0.1:{req.Port}/?token={req.Token}", browser.OpenedUrl);
        // ...but the printed confirmation is the clean URL (no token).
        Assert.Contains($"Opened http://127.0.0.1:{req.Port}/", result.StdOut);
        Assert.Contains("Launching Agent Sync UI", result.StdOut);
        Assert.DoesNotContain(req.Token, result.StdOut);
    }

    [Fact]
    public void Ui_BrowserOpenFails_PrintsTokenUrlOnStdout()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: "/opt/agent-sync-ui");
        var browser = new FakeBrowserLauncher(succeeds: false);

        var result = h.InvokeWithUi(launcher, browser, readiness: null, "ui");

        var req = launcher.Request!;
        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains($"Open http://127.0.0.1:{req.Port}/?token={req.Token}", result.StdOut);
    }

    [Fact]
    public void Ui_NoOpen_DoesNotOpenBrowser_PrintsTokenUrl()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: "/opt/agent-sync-ui");
        var browser = new FakeBrowserLauncher(succeeds: true);

        var result = h.InvokeWithUi(launcher, browser, readiness: null, "ui", "--no-open");

        var req = launcher.Request!;
        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Null(browser.OpenedUrl);
        Assert.Contains($"Open http://127.0.0.1:{req.Port}/?token={req.Token}", result.StdOut);
    }

    [Fact]
    public void Ui_ReadinessTimeout_Exits3()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: "/opt/agent-sync-ui");
        var readiness = new FakeReadinessProbe(ready: false);

        var result = h.InvokeWithUi(launcher, browser: null, readiness, "ui");

        Assert.Equal(ExitCodes.EnvironmentProblem, result.ExitCode);
        Assert.Contains("did not become ready", result.StdErr);
    }

    [Fact]
    public void Ui_ReadinessPolledOnLaunchedPort()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: "/opt/agent-sync-ui");
        var readiness = new FakeReadinessProbe(ready: true);

        var result = h.InvokeWithUi(launcher, browser: null, readiness, "ui");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Equal(launcher.Request!.Port, readiness.PolledPort);
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
                Assert.DoesNotContain("AgentSync.Ui.Abstractions", name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
