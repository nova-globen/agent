using AgentSync.Core;
using AgentSync.Core.Import;

namespace AgentSync.Core.Tests.Import;

public sealed class SourceDetectorTests
{
    private static void Write(string path, string content = "x")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void StandaloneSkillFile_IsSkillFile()
    {
        using var t = new TempDir();
        Write(Path.Combine(t.Path, "SKILL.md"));

        var src = SourceDetector.Detect(t.Path, "SKILL.md");

        Assert.Equal(ImportSourceKind.SkillFile, src.Kind);
    }

    [Fact]
    public void FolderWithSkillFile_IsSkillFolder()
    {
        using var t = new TempDir();
        Write(Path.Combine(t.Path, "mySkill", "SKILL.md"));

        var src = SourceDetector.Detect(t.Path, "mySkill");

        Assert.Equal(ImportSourceKind.SkillFolder, src.Kind);
    }

    [Fact]
    public void OpenAiSkillFolder_IsSkillFolder()
    {
        using var t = new TempDir();
        Write(Path.Combine(t.Path, ".chatgpt", "skills", "code-review", "SKILL.md"));

        var src = SourceDetector.Detect(t.Path, ".chatgpt/skills/code-review");

        Assert.Equal(ImportSourceKind.SkillFolder, src.Kind);
    }

    [Fact]
    public void ClaudeSkillFolder_IsSkillFolder()
    {
        using var t = new TempDir();
        Write(Path.Combine(t.Path, ".claude", "skills", "code-review", "SKILL.md"));

        var src = SourceDetector.Detect(t.Path, ".claude/skills/code-review");

        Assert.Equal(ImportSourceKind.SkillFolder, src.Kind);
    }

    [Fact]
    public void SkillsRoot_IsSkillsRoot()
    {
        using var t = new TempDir();
        Write(Path.Combine(t.Path, ".claude", "skills", "a", "SKILL.md"));

        var src = SourceDetector.Detect(t.Path, ".claude/skills");

        Assert.Equal(ImportSourceKind.SkillsRoot, src.Kind);
    }

    [Theory]
    [InlineData("AGENTS.md", ImportSourceKind.AgentsMd)]
    [InlineData("CLAUDE.md", ImportSourceKind.ClaudeMd)]
    [InlineData(".github/copilot-instructions.md", ImportSourceKind.Copilot)]
    [InlineData(".gemini/GEMINI.md", ImportSourceKind.Gemini)]
    public void InstructionFiles_AreDetected(string relPath, ImportSourceKind expected)
    {
        using var t = new TempDir();
        Write(Path.Combine(t.Path, relPath.Replace('/', Path.DirectorySeparatorChar)));

        var src = SourceDetector.Detect(t.Path, relPath);

        Assert.Equal(expected, src.Kind);
    }

    [Fact]
    public void CursorRuleFile_IsDetected()
    {
        using var t = new TempDir();
        Write(Path.Combine(t.Path, ".cursor", "rules", "code-review.mdc"));

        var src = SourceDetector.Detect(t.Path, ".cursor/rules/code-review.mdc");

        Assert.Equal(ImportSourceKind.CursorRuleFile, src.Kind);
    }

    [Fact]
    public void CursorRulesDir_IsDetected()
    {
        using var t = new TempDir();
        Write(Path.Combine(t.Path, ".cursor", "rules", "code-review.mdc"));

        var src = SourceDetector.Detect(t.Path, ".cursor/rules");

        Assert.Equal(ImportSourceKind.CursorRulesDir, src.Kind);
    }

    [Fact]
    public void MissingPath_IsMissing()
    {
        using var t = new TempDir();

        var src = SourceDetector.Detect(t.Path, "nope/SKILL.md");

        Assert.Equal(ImportSourceKind.Missing, src.Kind);
        Assert.NotNull(src.Reason);
    }

    [Fact]
    public void UnsupportedFile_IsUnsupported()
    {
        using var t = new TempDir();
        Write(Path.Combine(t.Path, "notes.md"));

        var src = SourceDetector.Detect(t.Path, "notes.md");

        Assert.Equal(ImportSourceKind.Unsupported, src.Kind);
        Assert.NotNull(src.Reason);
    }

    [Fact]
    public void UnsafePath_Throws()
    {
        using var t = new TempDir();

        Assert.Throws<RepoPathException>(() => SourceDetector.Detect(t.Path, "../escape/SKILL.md"));
    }
}
