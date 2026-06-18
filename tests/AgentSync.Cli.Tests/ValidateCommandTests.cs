using System.Text.Json;
using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class ValidateCommandTests
{
    [Fact]
    public void Validate_AfterInit_Succeeds()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("validate");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("valid", result.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_MissingConfig_ReturnsValidationFailed()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("validate");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
    }

    [Fact]
    public void Validate_Json_ReportsMessages()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("validate", "--json");

        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.False(doc.RootElement.GetProperty("valid").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("messages").GetArrayLength() >= 1);
    }
}
