using System.Text.Json;
using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class SessionsCommandTests
{
    [Fact]
    public void Providers_ListsBuiltins()
    {
        using var h = new CliTestHarness();
        var result = h.Invoke("sessions", "providers");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("claude", result.StdOut);
        Assert.Contains("codex", result.StdOut);
        Assert.Contains("copilot", result.StdOut);
        Assert.Contains("gemini", result.StdOut);
        Assert.Contains("cursor", result.StdOut);
    }

    [Fact]
    public void Providers_Json_IsValid()
    {
        using var h = new CliTestHarness();
        var result = h.Invoke("sessions", "providers", "--json");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.Equal(5, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void NoSubcommand_IsInvalidUsage()
    {
        using var h = new CliTestHarness();
        var result = h.Invoke("sessions");
        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void Backup_UnknownProvider_IsInvalidUsage()
    {
        using var h = new CliTestHarness();
        var result = h.Invoke("sessions", "backup", "notreal");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
        Assert.Contains("unknown session provider", result.StdErr);
    }

    [Fact]
    public void Backup_MissingProvider_IsInvalidUsage()
    {
        using var h = new CliTestHarness();
        var result = h.Invoke("sessions", "backup");
        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void Backup_NoSessionsForIsolatedProject_ReportsEmpty()
    {
        using var h = new CliTestHarness();
        // A project path that no agent has ever stored sessions for.
        var isolated = Path.Combine(h.WorkingDirectory, "no-such-project");
        var result = h.Invoke("sessions", "backup", "claude", "--project", isolated);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("No Claude Code sessions", result.StdOut);
    }

    [Fact]
    public void List_Json_IsValid()
    {
        using var h = new CliTestHarness();
        var isolated = Path.Combine(h.WorkingDirectory, "no-such-project");
        var result = h.Invoke("sessions", "list", "--project", isolated, "--json");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.Equal(5, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void Restore_MissingArchive_IsInvalidUsage()
    {
        using var h = new CliTestHarness();
        var result = h.Invoke("sessions", "restore");
        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void Restore_NonexistentArchive_IsInvalidUsage()
    {
        using var h = new CliTestHarness();
        var result = h.Invoke("sessions", "restore", "nope.zip");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
        Assert.Contains("does not exist", result.StdErr);
    }

    [Fact]
    public void GitAgentParity_SessionsProviders()
    {
        // The git-agent entry point delegates to the same CliRunner, so behavior matches.
        using var h = new CliTestHarness();
        var viaAgent = h.Invoke("sessions", "providers");
        var runner = new CliRunner(new StringWriter(), new StringWriter(), h.WorkingDirectory);
        Assert.Equal(ExitCodes.Success, runner.Run(new[] { "sessions", "providers" }));
        Assert.Equal(ExitCodes.Success, viaAgent.ExitCode);
    }
}
