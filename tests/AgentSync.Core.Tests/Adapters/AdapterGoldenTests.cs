using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;

namespace AgentSync.Core.Tests.Adapters;

public sealed class AdapterGoldenTests
{
    private static Skill SampleSkill() => new()
    {
        Manifest = new SkillManifest
        {
            Id = "code-review",
            Name = "Code Review",
            Description = "Reviews pull requests using project conventions.",
            Version = "0.1.0",
        },
        Body = "# Code Review\n\n## When to use\n\nWhen a pull request needs review.\n\n## Steps\n\n1. Check correctness.\n2. Check style.\n",
        DirectoryName = "code-review",
        DirectoryPath = "/tmp/code-review",
    };

    private static string Golden(string name)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden", name));

    private static string Lf(string s) => s.Replace("\r\n", "\n");

    [Theory]
    [InlineData(TargetIds.AgentsMd)]
    [InlineData(TargetIds.ClaudeMd)]
    [InlineData(TargetIds.Copilot)]
    [InlineData(TargetIds.Gemini)]
    public void SharedMarkdown_MatchesGolden(string targetId)
    {
        var adapter = new SharedMarkdownAdapter(targetId);

        var rendered = adapter.Render(SampleSkill());

        Assert.Equal(Lf(Golden("code-review.agents_md.md")).TrimEnd('\n'), Lf(rendered).TrimEnd('\n'));
    }

    [Fact]
    public void Cursor_MatchesGolden()
    {
        var rendered = new CursorAdapter().Render(SampleSkill());

        Assert.Equal(Lf(Golden("code-review.cursor.mdc")).TrimEnd('\n'), Lf(rendered).TrimEnd('\n'));
        Assert.EndsWith("\n", rendered); // whole-file targets are newline-terminated
    }

    [Theory]
    [InlineData(TargetIds.OpenAiSkill)]
    [InlineData(TargetIds.ClaudeSkill)]
    public void SkillFolder_MatchesGolden(string targetId)
    {
        var rendered = new SkillFolderAdapter(targetId).Render(SampleSkill());

        Assert.Equal(Lf(Golden("code-review.skill_folder.md")).TrimEnd('\n'), Lf(rendered).TrimEnd('\n'));
        Assert.StartsWith("---\nname: Code Review\n", Lf(rendered));
    }

    [Fact]
    public void SharedMarkdown_HasNoTrailingNewline()
    {
        // Shared sections are embedded between markers; the writer adds spacing.
        var rendered = new SharedMarkdownAdapter(TargetIds.AgentsMd).Render(SampleSkill());
        Assert.False(rendered.EndsWith("\n"));
    }

    [Fact]
    public void Render_IsDeterministic()
    {
        var a = new SkillFolderAdapter(TargetIds.ClaudeSkill).Render(SampleSkill());
        var b = new SkillFolderAdapter(TargetIds.ClaudeSkill).Render(SampleSkill());
        Assert.Equal(a, b);
    }

    [Fact]
    public void ResolvePath_PlacesDedicatedFilesCorrectly()
    {
        var skill = SampleSkill();
        Assert.Equal(".cursor/rules/code-review.mdc", new CursorAdapter().ResolvePath(".cursor/rules", skill));
        Assert.Equal(".claude/skills/code-review/SKILL.md",
            new SkillFolderAdapter(TargetIds.ClaudeSkill).ResolvePath(".claude/skills", skill));
        Assert.Equal("AGENTS.md", new SharedMarkdownAdapter(TargetIds.AgentsMd).ResolvePath("AGENTS.md", skill));
    }
}
