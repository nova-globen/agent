using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;

namespace AgentSync.Core.Tests.Adapters;

public sealed class ProjectionPlannerTests
{
    [Fact]
    public void Plan_DefaultInit_ProducesProjectionsForBothScaffoldedSkills()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        var ws = WorkspaceLoader.Load(temp.Path);

        var plan = new ProjectionPlanner().Plan(ws);

        // code-review projects to all 7 targets; using-agent-sync projects to claude_skill
        // only (its skill.yaml disables the rest), so 8 projections in total.
        Assert.Equal(8, plan.Count);
        Assert.Contains(plan, p => p.TargetId == TargetIds.AgentsMd && p.Mode == ProjectionMode.SharedSection);
        Assert.Contains(plan, p => p.TargetId == TargetIds.Cursor && p.Mode == ProjectionMode.WholeFile);
        Assert.Contains(plan, p => p.RelativePath == ".claude/skills/code-review/SKILL.md");
        Assert.Contains(plan, p => p.RelativePath == ".claude/skills/using-agent-sync/SKILL.md");
        // The using-agent-sync skill does not bloat the always-loaded shared files.
        Assert.DoesNotContain(plan, p => p.SkillId == "using-agent-sync" && p.TargetId == TargetIds.AgentsMd);
    }

    [Fact]
    public void Plan_RespectsConfigDisabledTarget()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        // Disable cursor in config.
        var cfgPath = Path.Combine(temp.Path, ".agent", "agent.yaml");
        var cfg = File.ReadAllText(cfgPath).Replace(
            "  cursor:\n    enabled: true\n    path: .cursor/rules",
            "  cursor:\n    enabled: false\n    path: .cursor/rules");
        File.WriteAllText(cfgPath, cfg);
        var ws = WorkspaceLoader.Load(temp.Path);

        var plan = new ProjectionPlanner().Plan(ws);

        Assert.DoesNotContain(plan, p => p.TargetId == TargetIds.Cursor);
    }

    [Fact]
    public void Plan_RespectsSkillDisabledTarget()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        var skillPath = Path.Combine(temp.Path, ".agent", "skills", "code-review", "skill.yaml");
        var skill = File.ReadAllText(skillPath).Replace(
            "  gemini:\n    enabled: true",
            "  gemini:\n    enabled: false");
        File.WriteAllText(skillPath, skill);
        var ws = WorkspaceLoader.Load(temp.Path);

        var plan = new ProjectionPlanner().Plan(ws);

        Assert.DoesNotContain(plan, p => p.TargetId == TargetIds.Gemini);
    }

    [Fact]
    public void Plan_IsDeterministicallyOrdered()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        var ws = WorkspaceLoader.Load(temp.Path);

        var first = new ProjectionPlanner().Plan(ws).Select(p => p.TargetId).ToArray();
        var second = new ProjectionPlanner().Plan(ws).Select(p => p.TargetId).ToArray();

        Assert.Equal(first, second);
        Assert.Equal(TargetIds.AgentsMd, first[0]); // canonical order
    }
}
