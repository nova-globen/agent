using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;
using AgentSync.Core.Import;

namespace AgentSync.Core.Tests.Import;

public sealed class ImportParsingTests
{
    // --- MarkdownFrontmatter --------------------------------------------------

    [Fact]
    public void Frontmatter_SplitsLeadingYaml()
    {
        var content = "---\nname: Code Review\ndescription: Reviews PRs.\n---\n\n## Body\n\ntext\n";

        var split = MarkdownFrontmatter.Split(content);

        Assert.True(split.HasFrontmatter);
        Assert.Contains("name: Code Review", split.Frontmatter);
        Assert.StartsWith("## Body", split.Body);
    }

    [Fact]
    public void Frontmatter_NoFrontmatter_ReturnsWholeBody()
    {
        var content = "## Body\n\ntext\n";

        var split = MarkdownFrontmatter.Split(content);

        Assert.False(split.HasFrontmatter);
        Assert.Equal(content, split.Body);
    }

    [Fact]
    public void Frontmatter_RoundTripsSkillFolderAdapterOutput()
    {
        var skill = MakeSkill("code-review", "Code Review", "Reviews changes.", "## When to use\n\nUse it.");
        var rendered = new SkillFolderAdapter(TargetIds.ClaudeSkill).Render(skill);

        var split = MarkdownFrontmatter.Split(rendered);
        var manifest = SkillManifest.Parse(split.Frontmatter!);

        Assert.Equal("Code Review", manifest.Name);
        Assert.Equal("Reviews changes.", manifest.Description);
        Assert.Contains("## When to use", split.Body);
    }

    // --- HeadingSplitter ------------------------------------------------------

    [Fact]
    public void HeadingSplitter_SplitsTopLevelSections()
    {
        var md = "# One\n\nalpha\n\n# Two\n\nbeta\n";

        var sections = HeadingSplitter.Split(md);

        Assert.Equal(2, sections.Count);
        Assert.Equal("One", sections[0].Title);
        Assert.Equal("alpha", sections[0].Body);
        Assert.Equal("Two", sections[1].Title);
        Assert.Equal("beta", sections[1].Body);
    }

    [Fact]
    public void HeadingSplitter_CapturesPreamble()
    {
        var md = "intro text\n\n## A\n\nbody\n";

        var sections = HeadingSplitter.Split(md);

        Assert.Equal(2, sections.Count);
        Assert.Null(sections[0].Title);
        Assert.Equal("intro text", sections[0].Body);
        Assert.Equal("A", sections[1].Title);
    }

    [Fact]
    public void HeadingSplitter_NoHeadings_ReturnsSingleSection()
    {
        var sections = HeadingSplitter.Split("just text\n");

        Assert.Single(sections);
        Assert.Null(sections[0].Title);
    }

    // --- IdInference ----------------------------------------------------------

    [Theory]
    [InlineData("Code Review", "code-review")]
    [InlineData("code-review", "code-review")]
    [InlineData("My_Cool Skill!", "my-cool-skill")]
    [InlineData("  Trim  Me  ", "trim-me")]
    public void IdInference_Slugifies(string input, string expected)
    {
        Assert.Equal(expected, IdInference.Slugify(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void IdInference_ReturnsNullForUnusable(string input)
    {
        Assert.Null(IdInference.Slugify(input));
    }

    [Theory]
    [InlineData("code-review", true)]
    [InlineData("Code-Review", false)]
    [InlineData("code--review", false)]
    [InlineData("-code", false)]
    public void IdInference_IsValid(string id, bool expected)
    {
        Assert.Equal(expected, IdInference.IsValid(id));
    }

    // --- SkillFolderReader ----------------------------------------------------

    [Fact]
    public void SkillFolderReader_ReadsFrontmatterAndBody()
    {
        using var t = new TempDir();
        var dir = Path.Combine(t.Path, "code-review");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"),
            "---\nname: Code Review\ndescription: Reviews PRs.\n---\n\n## When to use\n\nUse it.\n");

        var src = SourceDetector.Detect(t.Path, "code-review");
        var parsed = SkillFolderReader.Read(src);

        Assert.Equal("Code Review", parsed.Manifest.Name);
        Assert.Equal("Reviews PRs.", parsed.Manifest.Description);
        Assert.Equal("code-review", parsed.IdCandidate);
        Assert.Contains("## When to use", parsed.Body);
    }

    [Fact]
    public void SkillFolderReader_BareFile_UsesParentFolderForId()
    {
        using var t = new TempDir();
        var dir = Path.Combine(t.Path, "my-skill");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), "Just a body.\n");

        var src = SourceDetector.Detect(t.Path, "my-skill/SKILL.md");
        var parsed = SkillFolderReader.Read(src);

        Assert.Equal("my-skill", parsed.IdCandidate);
        Assert.Equal("Just a body.", parsed.Body.Trim());
    }

    [Fact]
    public void SkillFolderReader_MalformedFrontmatter_Throws()
    {
        using var t = new TempDir();
        var dir = Path.Combine(t.Path, "bad");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), "---\nname: [unclosed\n---\nbody\n");

        var src = SourceDetector.Detect(t.Path, "bad");

        Assert.Throws<ConfigParseException>(() => SkillFolderReader.Read(src));
    }

    // --- DraftValidation ------------------------------------------------------

    [Fact]
    public void DraftValidation_ValidDraft_IsValid()
    {
        var draft = new SkillDraft("code-review", "Code Review", "Reviews changes.", "0.1.0",
            "## When to use\n\nUse it.", new[] { TargetIds.AgentsMd }, "src/SKILL.md");

        var result = DraftValidation.Validate(draft);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void DraftValidation_InvalidId_Fails()
    {
        var draft = new SkillDraft("Bad Id", "Name", "Desc.", "0.1.0", "body",
            Array.Empty<string>(), "src/SKILL.md");

        var result = DraftValidation.Validate(draft);

        Assert.False(result.IsValid);
        Assert.Contains(result.Messages, m => m.Code == "skill.id-format");
    }

    [Fact]
    public void DraftValidation_EmptyDescription_Fails()
    {
        var draft = new SkillDraft("code-review", "Code Review", "", "0.1.0", "body",
            Array.Empty<string>(), "src/SKILL.md");

        var result = DraftValidation.Validate(draft);

        Assert.False(result.IsValid);
        Assert.Contains(result.Messages, m => m.Code == "skill.description-missing");
    }

    private static Skill MakeSkill(string id, string name, string description, string body)
        => new()
        {
            Manifest = new SkillManifest { Id = id, Name = name, Description = description, Version = "0.1.0" },
            Body = body,
            DirectoryName = id,
            DirectoryPath = id,
        };
}
