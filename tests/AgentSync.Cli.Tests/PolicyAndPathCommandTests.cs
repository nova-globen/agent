using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class PolicyAndPathCommandTests
{
    private static string ConfigPath(string root) => Path.Combine(root, ".agent", "agent.yaml");

    [Fact]
    public void Status_FailOnDrift_RespectsDisabledPolicy()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init"); // not synced → projections missing

        var failing = h.Invoke("status", "--fail-on-drift");
        Assert.Equal(ExitCodes.DriftOrValidationFailed, failing.ExitCode);

        var cfg = ConfigPath(h.WorkingDirectory);
        File.WriteAllText(cfg, File.ReadAllText(cfg)
            .Replace("fail_on_missing_projection: true", "fail_on_missing_projection: false"));

        var passing = h.Invoke("status", "--fail-on-drift");
        Assert.Equal(ExitCodes.Success, passing.ExitCode);
        // Still reported, just not failing.
        Assert.Contains("WARN", passing.StdOut);
    }

    [Fact]
    public void Status_UnsafeConfigPath_ReportsConfigError()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        var cfg = ConfigPath(h.WorkingDirectory);
        File.WriteAllText(cfg, File.ReadAllText(cfg)
            .Replace("path: AGENTS.md", "path: ../../escape/AGENTS.md"));

        var result = h.Invoke("status", "--fail-on-drift");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
        Assert.Contains("unsafe", result.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sync_UnsafeConfigPath_DoesNotWriteOutsideRoot()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        var cfg = ConfigPath(h.WorkingDirectory);
        File.WriteAllText(cfg, File.ReadAllText(cfg)
            .Replace("path: AGENTS.md", "path: /tmp/agent-sync-escape-AGENTS.md"));

        var result = h.Invoke("sync");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
        Assert.False(File.Exists("/tmp/agent-sync-escape-AGENTS.md"));
    }
}
