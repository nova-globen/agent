using System.Text.Json;
using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class SyncCommandTests
{
    [Fact]
    public void Sync_AfterInit_WritesProjectionsAndSucceeds()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("sync");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(h.WorkingDirectory, "AGENTS.md")));
        Assert.True(File.Exists(Path.Combine(h.WorkingDirectory, ".claude", "skills", "code-review", "SKILL.md")));
    }

    [Fact]
    public void Sync_Check_BeforeSync_ReportsDriftWithoutWriting()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("sync", "--check");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(h.WorkingDirectory, "AGENTS.md")));
    }

    [Fact]
    public void Sync_Check_AfterSync_IsClean()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("sync");

        var result = h.Invoke("sync", "--check");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
    }

    [Fact]
    public void Sync_InvalidConfig_Fails()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("sync");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
    }

    [Fact]
    public void Sync_Force_OverManualEdit_SucceedsAndReportsNoSkip()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("sync");

        // Hand-edit inside a generated section to trigger manual-edit detection.
        var agentsMd = Path.Combine(h.WorkingDirectory, "AGENTS.md");
        File.WriteAllText(agentsMd,
            File.ReadAllText(agentsMd).Replace("## Code Review", "## Code Review (hand edited)"));

        // Without --force the edit is left untouched and reported as drift.
        var skipped = h.Invoke("sync");
        Assert.Equal(ExitCodes.DriftOrValidationFailed, skipped.ExitCode);
        Assert.Contains("left untouched", skipped.StdOut);

        // With --force the section is rewritten, so sync succeeds and says nothing about skips.
        var forced = h.Invoke("sync", "--force");
        Assert.Equal(ExitCodes.Success, forced.ExitCode);
        Assert.DoesNotContain("left untouched", forced.StdOut);

        // The repository is back in sync.
        Assert.Equal(ExitCodes.Success, h.Invoke("status", "--fail-on-drift", "--ci").ExitCode);
    }

    [Fact]
    public void Sync_Json_ListsOutcomes()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("sync", "--json");

        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.True(doc.RootElement.GetProperty("configValid").GetBoolean());
        // code-review projects to all 7 targets; using-agent-sync projects to claude_skill
        // only, for 8 projections in total.
        Assert.Equal(8, doc.RootElement.GetProperty("outcomes").GetArrayLength());
    }
}
