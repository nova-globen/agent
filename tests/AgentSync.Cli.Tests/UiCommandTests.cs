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

        public string? LaunchedExe { get; private set; }
        public string? LaunchedRepo { get; private set; }

        public string? Locate() => _path;

        public bool Launch(string executablePath, string repoPath)
        {
            LaunchedExe = executablePath;
            LaunchedRepo = repoPath;
            return _launchSucceeds;
        }
    }

    [Fact]
    public void Ui_NotInstalled_ReportsCleanlyAndExits3()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: null);

        var result = h.InvokeWithUi(launcher, "ui");

        Assert.Equal(ExitCodes.EnvironmentProblem, result.ExitCode);
        Assert.Contains("Agent Sync UI is not installed.", result.StdOut);
        Assert.Contains("The headless CLI is working.", result.StdOut);
    }

    [Fact]
    public void Ui_Installed_LaunchesAndPassesRepoPath()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var launcher = new FakeUiLauncher(path: "/opt/agent-sync-ui");

        var result = h.InvokeWithUi(launcher, "ui");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("Launching Agent Sync UI", result.StdOut);
        Assert.Equal("/opt/agent-sync-ui", launcher.LaunchedExe);
        Assert.Equal(h.WorkingDirectory, launcher.LaunchedRepo);
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
        Assert.Equal(h.WorkingDirectory, launcher.LaunchedRepo);
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
    public void Cli_DoesNotReferenceMauiOrOpenMaui()
    {
        foreach (var assembly in new[] { typeof(CliRunner).Assembly, typeof(UiLauncher).Assembly })
        {
            foreach (var referenced in assembly.GetReferencedAssemblies())
            {
                var name = referenced.Name ?? string.Empty;
                Assert.DoesNotContain("maui", name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("openmaui", name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
