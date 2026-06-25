using System.Text.Json;
using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class TargetCrudCommandTests
{
    private static string ConfigPath(CliTestHarness h) => Path.Combine(h.WorkingDirectory, ".agent", "agent.yaml");

    [Fact]
    public void TargetList_Text()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("target", "list");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("agents_md", result.StdOut);
        Assert.Contains("claude_md", result.StdOut);
    }

    [Fact]
    public void Targets_Alias_Lists()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("targets");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("cursor", result.StdOut);
    }

    [Fact]
    public void TargetShow_Json()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("target", "show", "agents_md", "--json");

        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.Equal("agents_md", doc.RootElement.GetProperty("id").GetString());
        Assert.True(doc.RootElement.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void TargetAdd_KnownTarget_OnFreshConfig()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        // Minimal config with no targets so we can add one cleanly.
        Directory.CreateDirectory(Path.Combine(h.WorkingDirectory, ".agent"));
        File.WriteAllText(ConfigPath(h), "version: 1\ntargets:\npolicy:\n  fail_on_manual_edit: true\n");

        var result = h.Invoke("target", "add", "agents_md", "--path", "AGENTS.md");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("agents_md", File.ReadAllText(ConfigPath(h)));
    }

    [Fact]
    public void TargetAdd_UnknownTarget_Rejected()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("target", "add", "not_a_target", "--path", "X.md");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void TargetAdd_UnsafePath_Rejected()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        Directory.CreateDirectory(Path.Combine(h.WorkingDirectory, ".agent"));
        File.WriteAllText(ConfigPath(h), "version: 1\ntargets:\npolicy:\n  fail_on_manual_edit: true\n");

        var result = h.Invoke("target", "add", "agents_md", "--path", "../escape.md");

        Assert.Equal(ExitCodes.EnvironmentProblem, result.ExitCode);
    }

    [Fact]
    public void TargetAdd_Duplicate_Rejected()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("target", "add", "agents_md", "--path", "AGENTS.md");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
        Assert.Contains("already configured", result.StdOut);
    }

    [Fact]
    public void TargetEdit_Path()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("target", "edit", "agents_md", "--path", "docs/AGENTS.md");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("docs/AGENTS.md", File.ReadAllText(ConfigPath(h)));
    }

    [Fact]
    public void TargetEdit_Disable_PreservesOtherTargetsAndPolicy()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("target", "edit", "gemini", "--enabled", "false");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        var config = File.ReadAllText(ConfigPath(h));
        Assert.Contains("agents_md", config);
        Assert.Contains("fail_on_manual_edit", config);
        Assert.Contains("gemini:\n    enabled: false", config.Replace("\r\n", "\n"));
    }

    [Fact]
    public void TargetEdit_InvalidBool_Rejected()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("target", "edit", "agents_md", "--enabled", "maybe");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void TargetDelete_BeforeSync_Succeeds()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("target", "delete", "gemini");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.DoesNotContain("gemini", File.ReadAllText(ConfigPath(h)));
    }

    [Fact]
    public void TargetDelete_RefusesWithoutForce_WhenProjectionsExist()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("sync");

        var result = h.Invoke("target", "delete", "agents_md");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
        Assert.Contains("agents_md", File.ReadAllText(ConfigPath(h)));
    }

    [Fact]
    public void TargetDelete_Force_RemovesAndPrunesLockfile()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("sync");

        var result = h.Invoke("target", "delete", "agents_md", "--force");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        var lockJson = File.ReadAllText(Path.Combine(h.WorkingDirectory, ".agent", "lock.json"));
        Assert.DoesNotContain("agents_md", lockJson);
    }

    [Fact]
    public void TargetList_Json_OrderedByCanonical()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("target", "list", "--json");

        using var doc = JsonDocument.Parse(result.StdOut);
        // 7 skill targets + claude_agent (fixed) + toml_agent (configurable).
        Assert.Equal(9, doc.RootElement.GetArrayLength());
        Assert.Equal("agents_md", doc.RootElement[0].GetProperty("id").GetString());
        // claude_agent is always enabled with a fixed path.
        var claudeAgent = doc.RootElement.EnumerateArray().First(e => e.GetProperty("id").GetString() == "claude_agent");
        Assert.Equal(".claude/agents/<id>.md", claudeAgent.GetProperty("path").GetString());
        Assert.True(claudeAgent.GetProperty("enabled").GetBoolean());
        // toml_agent is present but not configured by default.
        var tomlAgent = doc.RootElement.EnumerateArray().Last();
        Assert.Equal("toml_agent", tomlAgent.GetProperty("id").GetString());
        Assert.False(tomlAgent.GetProperty("configured").GetBoolean());
    }

    [Fact]
    public void TargetList_Human_SurfacesSubagentTarget()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("target", "list");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("claude_agent", result.StdOut);
        Assert.Contains(".claude/agents/<id>.md", result.StdOut);
    }

    [Fact]
    public void TargetEdit_ThenValidate_Clean()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("target", "edit", "agents_md", "--path", "AGENTS.md", "--enabled", "true");

        var validate = h.Invoke("validate");

        Assert.Equal(ExitCodes.Success, validate.ExitCode);
    }
}
