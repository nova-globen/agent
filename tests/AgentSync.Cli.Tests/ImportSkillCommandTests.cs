using System.Text.Json;
using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class ImportSkillCommandTests
{
    private static string WriteSkillFile(string dir, string content)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "SKILL.md");
        File.WriteAllText(path, content);
        return path;
    }

    private const string FrontmatterSkill =
        "---\nname: Code Review\ndescription: Reviews changes.\n---\n\n## When to use\n\nUse it.\n";

    [Fact]
    public void ImportSkill_FromStandaloneFile_CreatesCanonicalSkill()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        WriteSkillFile(Path.Combine(h.WorkingDirectory, "incoming", "pr-review"), FrontmatterSkill);

        var result = h.Invoke("import", "skill", "incoming/pr-review/SKILL.md");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        var dir = Path.Combine(h.WorkingDirectory, ".agent", "skills", "pr-review");
        Assert.True(File.Exists(Path.Combine(dir, "skill.yaml")));
        Assert.True(File.Exists(Path.Combine(dir, "SKILL.md")));
        Assert.Contains("name: Code Review", File.ReadAllText(Path.Combine(dir, "skill.yaml")));
    }

    [Fact]
    public void ImportSkill_FromFolder_CreatesCanonicalSkill()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        WriteSkillFile(Path.Combine(h.WorkingDirectory, ".chatgpt", "skills", "triage"), FrontmatterSkill);

        var result = h.Invoke("import", "skill", ".chatgpt/skills/triage");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "triage", "skill.yaml")));
    }

    [Fact]
    public void ImportSkill_FromClaudeSkillFolder_Works()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        WriteSkillFile(Path.Combine(h.WorkingDirectory, ".claude", "skills", "docs"), FrontmatterSkill);

        var result = h.Invoke("import", "skill", ".claude/skills/docs");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "docs", "skill.yaml")));
    }

    [Fact]
    public void ImportSkill_InfersIdFromFolder()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        WriteSkillFile(Path.Combine(h.WorkingDirectory, "src", "my-skill"), "Body only, no frontmatter.\n");

        var result = h.Invoke("import", "skill", "src/my-skill");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(Directory.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "my-skill")));
    }

    [Fact]
    public void ImportSkill_ExplicitId_Overrides()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        WriteSkillFile(Path.Combine(h.WorkingDirectory, "src", "x"), FrontmatterSkill);

        var result = h.Invoke("import", "skill", "src/x", "--id", "renamed-skill");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(Directory.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "renamed-skill")));
    }

    [Fact]
    public void ImportSkill_ExplicitName_Overrides()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        WriteSkillFile(Path.Combine(h.WorkingDirectory, "src", "pr-review"), FrontmatterSkill);

        h.Invoke("import", "skill", "src/pr-review", "--name", "Custom Name");

        var yaml = File.ReadAllText(Path.Combine(h.WorkingDirectory, ".agent", "skills", "pr-review", "skill.yaml"));
        Assert.Contains("name: Custom Name", yaml);
    }

    [Fact]
    public void ImportSkill_DryRun_WritesNothing()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        WriteSkillFile(Path.Combine(h.WorkingDirectory, "src", "pr-review"), FrontmatterSkill);

        var result = h.Invoke("import", "skill", "src/pr-review", "--dry-run");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.False(Directory.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "pr-review")));
        Assert.Contains("would create", result.StdOut);
    }

    [Fact]
    public void ImportSkill_NonForce_RefusesOverwrite()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        WriteSkillFile(Path.Combine(h.WorkingDirectory, "src", "pr-review"), FrontmatterSkill);
        h.Invoke("import", "skill", "src/pr-review");

        var result = h.Invoke("import", "skill", "src/pr-review");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
        Assert.Contains("already exists", result.StdOut);
    }

    [Fact]
    public void ImportSkill_Force_Overwrites()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        WriteSkillFile(Path.Combine(h.WorkingDirectory, "src", "pr-review"), FrontmatterSkill);
        h.Invoke("import", "skill", "src/pr-review");

        var result = h.Invoke("import", "skill", "src/pr-review", "--force");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("overwritten", result.StdOut);
    }

    [Fact]
    public void ImportSkill_MissingPath_InvalidUsage()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("import", "skill", "nope/SKILL.md");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void ImportSkill_UnknownTarget_InvalidUsage()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        WriteSkillFile(Path.Combine(h.WorkingDirectory, "src", "pr-review"), FrontmatterSkill);

        var result = h.Invoke("import", "skill", "src/pr-review", "--target", "not_a_target");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void ImportSkill_NoPath_InvalidUsage()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("import", "skill");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void ImportSkill_Json_IsDeterministic()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        WriteSkillFile(Path.Combine(h.WorkingDirectory, "src", "pr-review"), FrontmatterSkill);

        var result = h.Invoke("import", "skill", "src/pr-review", "--json");

        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.Equal("Ok", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal("pr-review", doc.RootElement.GetProperty("items")[0].GetProperty("id").GetString());
    }

    [Fact]
    public void ImportSkill_ThenSync_IsClean()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        WriteSkillFile(Path.Combine(h.WorkingDirectory, "src", "extra-skill"), FrontmatterSkill);
        h.Invoke("import", "skill", "src/extra-skill");

        h.Invoke("sync");
        var status = h.Invoke("status", "--fail-on-drift", "--ci");

        Assert.Equal(ExitCodes.Success, status.ExitCode);
    }
}
