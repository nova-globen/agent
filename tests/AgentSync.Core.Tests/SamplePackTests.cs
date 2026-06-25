namespace AgentSync.Core.Tests;

public sealed class SamplePackTests
{
    [Fact]
    public void GetSkills_ReturnsExpectedIds()
    {
        var ids = SamplePack.GetSkills().Select(s => s.Id).ToHashSet();

        Assert.Contains("adr-author", ids);
        Assert.Contains("agentsync", ids);
        Assert.Contains("autopilot", ids);
        Assert.Contains("commit-governor", ids);
        Assert.Contains("dotnet-inspect", ids);
        Assert.Contains("memory-curator", ids);
        Assert.Contains("next-step", ids);
        Assert.Contains("operating-guide", ids);
        Assert.Contains("plan-governor", ids);
    }

    [Fact]
    public void GetSkills_EachSkillHasNonEmptyYamlAndMarkdown()
    {
        foreach (var skill in SamplePack.GetSkills())
        {
            Assert.False(string.IsNullOrWhiteSpace(skill.SkillYaml),
                $"skill '{skill.Id}' has empty YAML");
            Assert.False(string.IsNullOrWhiteSpace(skill.SkillMd),
                $"skill '{skill.Id}' has empty SKILL.md");
        }
    }

    [Fact]
    public void GetSkills_YamlContainsExpectedId()
    {
        foreach (var skill in SamplePack.GetSkills())
        {
            Assert.Contains($"id: {skill.Id}", skill.SkillYaml);
        }
    }

    [Fact]
    public void GetSkills_OperatingGuideTargetsAgentsMd()
    {
        var og = SamplePack.GetSkills().Single(s => s.Id == "operating-guide");
        Assert.Contains("agents_md:", og.SkillYaml);
        Assert.Contains("enabled: true", og.SkillYaml);
    }

    [Fact]
    public void GetAgents_ReturnsExpectedIds()
    {
        var ids = SamplePack.GetAgents().Select(a => a.Id).ToHashSet();

        Assert.Contains("planner", ids);
        Assert.Contains("verifier", ids);
        Assert.Contains("git-ops-executor", ids);
    }

    [Fact]
    public void GetAgents_EachAgentHasNonEmptyYamlAndMarkdown()
    {
        foreach (var agent in SamplePack.GetAgents())
        {
            Assert.False(string.IsNullOrWhiteSpace(agent.AgentYaml),
                $"agent '{agent.Id}' has empty YAML");
            Assert.False(string.IsNullOrWhiteSpace(agent.AgentMd),
                $"agent '{agent.Id}' has empty AGENT.md");
        }
    }

    [Fact]
    public void GetHooks_ReturnsExpectedNames()
    {
        var names = SamplePack.GetHooks().Select(h => h.Name).ToHashSet();

        Assert.Contains("pre-commit", names);
        Assert.Contains("commit-msg", names);
        Assert.Contains("pre-push", names);
        Assert.Contains("post-checkout", names);
        Assert.Contains("post-merge", names);
    }

    [Fact]
    public void GetHooks_AllHooksAreMarkedExecutable()
    {
        Assert.All(SamplePack.GetHooks(), h => Assert.True(h.Executable));
    }

    [Fact]
    public void GetHooks_HooksUseBashShebang()
    {
        foreach (var hook in SamplePack.GetHooks())
        {
            Assert.True(hook.Content.TrimStart().StartsWith("#!/usr/bin/env bash"),
                $"hook '{hook.Name}' does not start with bash shebang");
        }
    }
}
