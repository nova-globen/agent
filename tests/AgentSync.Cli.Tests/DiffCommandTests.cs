using System.Text.Json;
using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class DiffCommandTests
{
    [Fact]
    public void Diff_AfterSync_NoDifferences()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("sync");

        var result = h.Invoke("diff");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("in sync", result.StdOut);
    }

    [Fact]
    public void Diff_BeforeSync_ShowsDifferencesAndExitsNonZero()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("diff");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
        Assert.Contains("AGENTS.md", result.StdOut);
    }

    [Fact]
    public void Diff_Json_ListsEntries()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("diff", "--json");

        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.True(doc.RootElement.GetProperty("hasDifferences").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("entries").GetArrayLength() > 0);
    }
}
