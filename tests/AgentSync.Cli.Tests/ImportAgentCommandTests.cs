using System.Text.Json;
using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class ImportAgentCommandTests
{
    private static void Write(CliTestHarness h, string relPath, string content)
    {
        var full = Path.Combine(h.WorkingDirectory, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static bool SkillExists(CliTestHarness h, string id)
        => Directory.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", id));

    [Fact]
    public void ImportAgent_AgentsMd_File()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, "legacy/AGENTS.md", "# Team Guide\n\nFollow the house style.\n");

        var result = h.Invoke("import", "agent", "legacy/AGENTS.md");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(SkillExists(h, "agents"));
    }

    [Fact]
    public void ImportAgent_ClaudeMd_File()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, "legacy/CLAUDE.md", "# Claude Notes\n\nBe concise.\n");

        var result = h.Invoke("import", "agent", "legacy/CLAUDE.md");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(SkillExists(h, "claude"));
    }

    [Fact]
    public void ImportAgent_Copilot_File()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, ".github/copilot-instructions.md", "# Copilot\n\nUse repo conventions.\n");

        var result = h.Invoke("import", "agent", ".github/copilot-instructions.md");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(SkillExists(h, "copilot-instructions"));
    }

    [Fact]
    public void ImportAgent_Gemini_File()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, "legacy/GEMINI.md", "# Gemini\n\nGuidance here.\n");

        var result = h.Invoke("import", "agent", "legacy/GEMINI.md");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(SkillExists(h, "gemini"));
    }

    [Fact]
    public void ImportAgent_CursorRuleFile()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, "legacy/.cursor/rules/triage.mdc",
            "---\ndescription: Triage rule.\nglobs:\nalwaysApply: false\n---\n\n# Triage\n\nTriage incoming issues.\n");

        var result = h.Invoke("import", "agent", "legacy/.cursor/rules/triage.mdc");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(SkillExists(h, "triage"));
    }

    [Fact]
    public void ImportAgent_CursorRulesDir_MultipleSkills()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, "legacy/.cursor/rules/one.mdc", "---\ndescription: One.\n---\n\n# One\n\nFirst.\n");
        Write(h, "legacy/.cursor/rules/two.mdc", "---\ndescription: Two.\n---\n\n# Two\n\nSecond.\n");

        var result = h.Invoke("import", "agent", "legacy/.cursor/rules");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(SkillExists(h, "one"));
        Assert.True(SkillExists(h, "two"));
    }

    [Fact]
    public void ImportAgent_ChatgptSkillsRoot_DelegatesToSkillImport()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, ".chatgpt/skills/triage/SKILL.md", "---\nname: Triage\ndescription: Triage.\n---\n\n## Use\n\nx\n");
        Write(h, ".chatgpt/skills/audit/SKILL.md", "---\nname: Audit\ndescription: Audit.\n---\n\n## Use\n\ny\n");

        var result = h.Invoke("import", "agent", ".chatgpt/skills");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(SkillExists(h, "triage"));
        Assert.True(SkillExists(h, "audit"));
    }

    [Fact]
    public void ImportAgent_ClaudeSkillsRoot_DelegatesToSkillImport()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, ".claude/skills/docs/SKILL.md", "---\nname: Docs\ndescription: Docs.\n---\n\n## Use\n\nz\n");

        var result = h.Invoke("import", "agent", ".claude/skills");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(SkillExists(h, "docs"));
    }

    [Fact]
    public void ImportAgent_SplitSections_CreatesSkillPerHeading()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, "legacy/AGENTS.md", "# Alpha\n\nFirst rule.\n\n# Beta\n\nSecond rule.\n");

        var result = h.Invoke("import", "agent", "legacy/AGENTS.md", "--split", "sections");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(SkillExists(h, "alpha"));
        Assert.True(SkillExists(h, "beta"));
    }

    [Fact]
    public void ImportAgent_MarkerAware_SkipsGeneratedSections()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        var content =
            "# Hand Written\n\nKeep me.\n\n" +
            "<!-- agent-sync:start id=code-review target=agents_md hash=sha256:abc -->\n" +
            "## Generated\n\nDo not import this.\n" +
            "<!-- agent-sync:end -->\n";
        Write(h, "legacy/AGENTS.md", content);

        var result = h.Invoke("import", "agent", "legacy/AGENTS.md", "--split", "sections", "--json");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        using var doc = JsonDocument.Parse(result.StdOut);
        var ids = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetString()).ToList();
        Assert.Contains("hand-written", ids);
        Assert.DoesNotContain("generated", ids);
    }

    [Fact]
    public void ImportAgent_DoesNotModifySource()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        var original = "# Team Guide\n\nFollow the house style.\n";
        Write(h, "legacy/AGENTS.md", original);

        h.Invoke("import", "agent", "legacy/AGENTS.md");

        Assert.Equal(original, File.ReadAllText(Path.Combine(h.WorkingDirectory, "legacy", "AGENTS.md")));
    }

    [Fact]
    public void ImportAgent_DryRun_WritesNothing()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, "legacy/AGENTS.md", "# Guide\n\ntext\n");

        var result = h.Invoke("import", "agent", "legacy/AGENTS.md", "--dry-run");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.False(SkillExists(h, "agents"));
    }

    [Fact]
    public void ImportAgent_Force_Overwrites()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, "legacy/AGENTS.md", "# Guide\n\ntext\n");
        h.Invoke("import", "agent", "legacy/AGENTS.md");

        var result = h.Invoke("import", "agent", "legacy/AGENTS.md", "--force");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("overwritten", result.StdOut);
    }

    [Fact]
    public void ImportAgent_TypeOverride_OnGenericMarkdown()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, "legacy/instructions.md", "# Generic\n\nSome guidance.\n");

        var result = h.Invoke("import", "agent", "legacy/instructions.md", "--type", "agents_md");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(SkillExists(h, "instructions"));
    }

    [Fact]
    public void ImportAgent_UnknownType_InvalidUsage()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, "legacy/AGENTS.md", "# Guide\n\ntext\n");

        var result = h.Invoke("import", "agent", "legacy/AGENTS.md", "--type", "bogus");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void ImportAgent_MissingPath_InvalidUsage()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("import", "agent", "nope.md");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void ImportAgent_UnsafePath_Environment()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("import", "agent", "../escape/AGENTS.md");

        Assert.Equal(ExitCodes.EnvironmentProblem, result.ExitCode);
    }

    [Fact]
    public void ImportAgent_ThenSync_IsClean()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Write(h, "legacy/AGENTS.md", "# Team Guide\n\nFollow the house style.\n");
        h.Invoke("import", "agent", "legacy/AGENTS.md");

        h.Invoke("sync");
        var status = h.Invoke("status", "--fail-on-drift", "--ci");

        Assert.Equal(ExitCodes.Success, status.ExitCode);
    }
}
