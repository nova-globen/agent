using AgentSync.Core.Autopilot;

namespace AgentSync.Core.Tests.Autopilot;

public sealed class AutopilotPreflightCheckerTests : IDisposable
{
    private readonly string _dir;

    public AutopilotPreflightCheckerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    // ── missing agent.yaml ────────────────────────────────────────────────────

    [Fact]
    public void Check_NoAgentYaml_ReturnsBlocker()
    {
        var result = new AutopilotPreflightChecker(_dir).Check();

        Assert.True(result.HasBlockers);
        Assert.Contains(result.Issues, i => i.Code == "preflight.no-agent-yaml");
    }

    [Fact]
    public void Check_NoAgentYaml_OnlyOneIssue_EarlyReturn()
    {
        // When agent.yaml is missing, further checks are skipped.
        var result = new AutopilotPreflightChecker(_dir).Check();

        Assert.Single(result.Issues);
    }

    // ── agent.yaml present but skill / prompts missing ───────────────────────

    [Fact]
    public void Check_AgentYamlPresent_NoAutopilotSkill_ReturnsWarning()
    {
        CreateAgentYaml();

        var result = new AutopilotPreflightChecker(_dir).Check();

        Assert.False(result.HasBlockers);
        Assert.Contains(result.Issues, i => i.Code == "preflight.no-autopilot-skill");
    }

    [Fact]
    public void Check_CanonicalSkillPresent_NoSkillWarning()
    {
        CreateAgentYaml();
        Directory.CreateDirectory(Path.Combine(_dir, ".agent", "skills", "autopilot"));

        var result = new AutopilotPreflightChecker(_dir).Check();

        Assert.DoesNotContain(result.Issues, i => i.Code == "preflight.no-autopilot-skill");
    }

    [Fact]
    public void Check_ProjectedSkillPresent_NoSkillWarning()
    {
        CreateAgentYaml();
        var projectedPath = Path.Combine(_dir, ".claude", "skills", "autopilot");
        Directory.CreateDirectory(projectedPath);
        File.WriteAllText(Path.Combine(projectedPath, "SKILL.md"), "# autopilot");

        var result = new AutopilotPreflightChecker(_dir).Check();

        Assert.DoesNotContain(result.Issues, i => i.Code == "preflight.no-autopilot-skill");
    }

    [Fact]
    public void Check_NoPromptsDir_ReturnsWarning()
    {
        CreateAgentYaml();
        Directory.CreateDirectory(Path.Combine(_dir, ".agent", "skills", "autopilot"));

        var result = new AutopilotPreflightChecker(_dir).Check();

        Assert.Contains(result.Issues, i => i.Code == "preflight.no-prompts-dir");
    }

    [Fact]
    public void Check_PromptsDirExistsButEmpty_ReturnsWarning()
    {
        CreateAgentYaml();
        Directory.CreateDirectory(Path.Combine(_dir, ".agent", "skills", "autopilot"));
        Directory.CreateDirectory(Path.Combine(_dir, ".agent", "prompts", "autopilot"));

        var result = new AutopilotPreflightChecker(_dir).Check();

        Assert.Contains(result.Issues, i => i.Code == "preflight.no-prompts");
        Assert.DoesNotContain(result.Issues, i => i.Code == "preflight.no-prompts-dir");
    }

    [Fact]
    public void Check_PromptFilePresent_NoPromptsWarning()
    {
        CreateAgentYaml();
        Directory.CreateDirectory(Path.Combine(_dir, ".agent", "skills", "autopilot"));
        var promptsDir = Path.Combine(_dir, ".agent", "prompts", "autopilot");
        Directory.CreateDirectory(promptsDir);
        File.WriteAllText(Path.Combine(promptsDir, "prompt-20260101-0000_start.txt"), "BOOTSTRAP: ...");

        var result = new AutopilotPreflightChecker(_dir).Check();

        Assert.DoesNotContain(result.Issues, i =>
            i.Code is "preflight.no-prompts-dir" or "preflight.no-prompts");
    }

    // ── HasIssues convenience ─────────────────────────────────────────────────

    [Fact]
    public void Check_AllPresent_HasNoIssues()
    {
        CreateAgentYaml();
        Directory.CreateDirectory(Path.Combine(_dir, ".agent", "skills", "autopilot"));
        var promptsDir = Path.Combine(_dir, ".agent", "prompts", "autopilot");
        Directory.CreateDirectory(promptsDir);
        File.WriteAllText(Path.Combine(promptsDir, "prompt-20260101-0000_start.txt"), "BOOTSTRAP: ...");

        var result = new AutopilotPreflightChecker(_dir).Check();

        // May still have a drift warning if StatusService finds invalid config,
        // but the skill/prompt blockers are gone.
        Assert.DoesNotContain(result.Issues, i => i.Severity == PreflightIssueSeverity.Blocker);
        Assert.DoesNotContain(result.Issues, i =>
            i.Code is "preflight.no-autopilot-skill" or "preflight.no-prompts-dir" or "preflight.no-prompts");
    }

    // ── FixCommand populated for actionable issues ────────────────────────────

    [Fact]
    public void Check_NoAgentYaml_FixCommandIsInit()
    {
        var result = new AutopilotPreflightChecker(_dir).Check();

        var issue = Assert.Single(result.Issues, i => i.Code == "preflight.no-agent-yaml");
        Assert.Equal("agent init --with-samples", issue.FixCommand);
    }

    [Fact]
    public void Check_NoAutopilotSkill_FixCommandIsInit()
    {
        CreateAgentYaml();

        var result = new AutopilotPreflightChecker(_dir).Check();

        var issue = Assert.Single(result.Issues, i => i.Code == "preflight.no-autopilot-skill");
        Assert.Equal("agent init --with-samples", issue.FixCommand);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private void CreateAgentYaml()
    {
        var agentDir = Path.Combine(_dir, ".agent");
        Directory.CreateDirectory(agentDir);
        File.WriteAllText(Path.Combine(agentDir, "agent.yaml"),
            "version: 1\ntargets:\n  claude_skill:\n    enabled: true\n    path: .claude/skills\n");
    }
}
