using System.Text.Json;
using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class CliRunnerTests
{
    [Fact]
    public void Version_PrintsVersionAndSucceeds()
    {
        using var h = new CliTestHarness();

        var result = h.Invoke("--version");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        // Assert the format, not a hardcoded version, so this survives version bumps.
        Assert.Matches(@"^agent \d+\.\d+\.\d+", result.StdOut.Trim());
    }

    [Fact]
    public void NoArgs_PrintsHelpAndSucceeds()
    {
        using var h = new CliTestHarness();

        var result = h.Invoke();

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("Usage:", result.StdOut);
    }

    [Fact]
    public void UnknownCommand_ReturnsInvalidUsage()
    {
        using var h = new CliTestHarness();

        var result = h.Invoke("frobnicate");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
        Assert.Contains("unknown command", result.StdErr);
    }

    [Fact]
    public void UnknownOption_ReturnsInvalidUsage()
    {
        using var h = new CliTestHarness();

        var result = h.Invoke("status", "--nope");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
        Assert.Contains("unknown option", result.StdErr);
    }

    [Fact]
    public void Init_ScaffoldsStructureAndSucceeds()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("init");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(h.WorkingDirectory, ".agent", "agent.yaml")));
        Assert.True(File.Exists(Path.Combine(h.WorkingDirectory, ".githooks", "pre-commit")));
    }

    [Fact]
    public void Status_NotInitialized_NoFailFlag_StillSucceeds()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("status");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("Initialized: no", result.StdOut);
    }

    [Fact]
    public void Status_NotInitialized_WithFailOnDrift_ReturnsDriftCode()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("status", "--fail-on-drift", "--ci");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
    }

    [Fact]
    public void Status_AfterInit_WithFailOnDrift_Succeeds()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("sync");

        var result = h.Invoke("status", "--fail-on-drift");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("No issues detected.", result.StdOut);
    }

    [Fact]
    public void Status_Json_IsValidJsonWithExpectedFields()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("sync");

        var result = h.Invoke("status", "--json");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.True(doc.RootElement.GetProperty("initialized").GetBoolean());
        // init scaffolds two skills: code-review and using-agent-sync.
        Assert.Equal(2, doc.RootElement.GetProperty("skills").GetInt32());
        Assert.False(doc.RootElement.GetProperty("hasProblems").GetBoolean());
    }

    [Fact]
    public void Doctor_Json_ReportsChecks()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("doctor", "--json");

        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.True(doc.RootElement.TryGetProperty("checks", out var checks));
        Assert.True(checks.GetArrayLength() >= 4);
    }
}
