using System.Text.Json;
using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class SkillCrudCommandTests
{
    private static string SkillDir(CliTestHarness h, string id)
        => Path.Combine(h.WorkingDirectory, ".agent", "skills", id);

    [Fact]
    public void SkillAdd_CreatesValidSkill()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skill", "add", "docs-review", "--name", "Docs Review", "--description", "Reviews docs.");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(SkillDir(h, "docs-review"), "skill.yaml")));
        Assert.True(File.Exists(Path.Combine(SkillDir(h, "docs-review"), "SKILL.md")));
        Assert.Equal(ExitCodes.Success, h.Invoke("validate").ExitCode);
    }

    [Fact]
    public void SkillAdd_InvalidId_Rejected()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skill", "add", "Bad Id", "--name", "X", "--description", "Y.");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void SkillAdd_Duplicate_Rejected()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skill", "add", "code-review", "--name", "X", "--description", "Y.");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
        Assert.Contains("already exists", result.StdOut);
    }

    [Fact]
    public void SkillAdd_MissingName_InvalidUsage()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skill", "add", "docs-review", "--description", "Y.");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void SkillEdit_NameAndDescription()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skill", "edit", "code-review", "--name", "Reviewer", "--description", "New desc.");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        var yaml = File.ReadAllText(Path.Combine(SkillDir(h, "code-review"), "skill.yaml"));
        Assert.Contains("name: Reviewer", yaml);
        Assert.Contains("description: New desc.", yaml);
    }

    [Fact]
    public void SkillEdit_BodyFromFile()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        var bodyPath = Path.Combine(h.WorkingDirectory, "new-body.md");
        File.WriteAllText(bodyPath, "## Fresh\n\nNew body content.\n");

        var result = h.Invoke("skill", "edit", "code-review", "--body-file", "new-body.md");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        var body = File.ReadAllText(Path.Combine(SkillDir(h, "code-review"), "SKILL.md"));
        Assert.Contains("New body content.", body);
    }

    [Fact]
    public void SkillEdit_AbsoluteBodyFile_Works()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        var bodyPath = Path.Combine(h.WorkingDirectory, "fresh-body.md");
        File.WriteAllText(bodyPath, "## Fresh\n\nAbsolute path body.\n");

        // Absolute --body-file paths used to be rejected as unsafe; they now resolve.
        var result = h.Invoke("skill", "edit", "code-review", "--body-file", bodyPath);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        var body = File.ReadAllText(Path.Combine(SkillDir(h, "code-review"), "SKILL.md"));
        Assert.Contains("Absolute path body.", body);
    }

    [Fact]
    public void SkillAdd_Help_PrintsUsageAndExitsZero()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("skill", "add", "--help");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("Usage: agent skill add", result.StdOut);
        Assert.Contains("--description", result.StdOut);
        Assert.Contains("--target", result.StdOut);
    }

    [Fact]
    public void SkillEdit_Missing_NotFound()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skill", "edit", "nope", "--name", "X");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
    }

    [Fact]
    public void SkillDelete_RefusesWithoutForce_WhenProjectionsExist()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("sync");

        var result = h.Invoke("skill", "delete", "code-review");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
        Assert.True(Directory.Exists(SkillDir(h, "code-review")));
    }

    [Fact]
    public void SkillDelete_BeforeSync_Succeeds()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skill", "delete", "code-review");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.False(Directory.Exists(SkillDir(h, "code-review")));
    }

    [Fact]
    public void SkillDelete_Force_RemovesSkillAndPrunesLockfile()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("sync");

        var result = h.Invoke("skill", "delete", "code-review", "--force");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.False(Directory.Exists(SkillDir(h, "code-review")));
        var lockJson = File.ReadAllText(Path.Combine(h.WorkingDirectory, ".agent", "lock.json"));
        Assert.DoesNotContain("code-review", lockJson);
    }

    [Fact]
    public void SkillDelete_DryRun_WritesNothing()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skill", "delete", "code-review", "--dry-run");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(Directory.Exists(SkillDir(h, "code-review")));
    }

    [Fact]
    public void SkillList_Text()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skill", "list");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("code-review", result.StdOut);
    }

    [Fact]
    public void Skills_Alias_ListsSkills()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skills");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("code-review", result.StdOut);
    }

    [Fact]
    public void SkillList_Json()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skill", "list", "--json");

        using var doc = JsonDocument.Parse(result.StdOut);
        // init scaffolds two skills: code-review and using-agent-sync (ordered by id).
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("code-review", doc.RootElement[0].GetProperty("id").GetString());
        Assert.Equal("using-agent-sync", doc.RootElement[1].GetProperty("id").GetString());
    }

    [Fact]
    public void SkillShow_Text()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skill", "show", "code-review");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("Skill: code-review", result.StdOut);
    }

    [Fact]
    public void SkillShow_Json()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skill", "show", "code-review", "--json");

        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.Equal("code-review", doc.RootElement.GetProperty("id").GetString());
        Assert.True(doc.RootElement.GetProperty("body").GetString()!.Length > 0);
    }

    [Fact]
    public void SkillShow_Missing_Fails()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("skill", "show", "nope");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
    }

    [Fact]
    public void SkillAdd_ThenSync_StatusClean()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("skill", "add", "docs-review", "--name", "Docs Review", "--description", "Reviews docs.");

        h.Invoke("sync");
        var status = h.Invoke("status", "--fail-on-drift", "--ci");

        Assert.Equal(ExitCodes.Success, status.ExitCode);
    }
}
