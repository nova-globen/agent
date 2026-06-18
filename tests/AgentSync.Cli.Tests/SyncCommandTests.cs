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
    public void Sync_Json_ListsOutcomes()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("sync", "--json");

        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.True(doc.RootElement.GetProperty("configValid").GetBoolean());
        Assert.Equal(7, doc.RootElement.GetProperty("outcomes").GetArrayLength());
    }
}
