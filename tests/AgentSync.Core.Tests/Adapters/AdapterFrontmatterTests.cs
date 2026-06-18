using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;
using YamlDotNet.Serialization;

namespace AgentSync.Core.Tests.Adapters;

public sealed class AdapterFrontmatterTests
{
    private static readonly IDeserializer Parser = new DeserializerBuilder().Build();

    private static Skill SkillWith(string name, string description) => new()
    {
        Manifest = new SkillManifest
        {
            Id = "code-review",
            Name = name,
            Description = description,
            Version = "0.1.0",
        },
        Body = "Body text.\n",
        DirectoryName = "code-review",
        DirectoryPath = "/tmp/code-review",
    };

    private static Dictionary<string, object> ParseFrontmatter(string rendered)
    {
        var text = rendered.Replace("\r\n", "\n");
        Assert.StartsWith("---\n", text);
        var end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        Assert.True(end > 0, "frontmatter is not terminated");
        var yaml = text[4..end];
        return Parser.Deserialize<Dictionary<string, object>>(yaml) ?? new();
    }

    public static IEnumerable<object[]> TrickyValues() => new[]
    {
        new object[] { "Name: with colon", "Desc: with colon and more" },
        new object[] { "Name # with hash", "Desc # comment-like" },
        new object[] { "Name with \"double quotes\"", "Desc with 'single quotes'" },
        new object[] { "Name: # both", "A: b # c" },
        new object[] { "Trailing space ", " Leading space" },
        new object[] { "Tabs\tand\tstuff", "back\\slash and {braces} [brackets]" },
        new object[] { "Plain Name", "A description with a newline\nsecond line" },
    };

    [Theory]
    [MemberData(nameof(TrickyValues))]
    public void SkillFolder_FrontmatterParsesAndRoundTrips(string name, string description)
    {
        var rendered = new SkillFolderAdapter(TargetIds.ClaudeSkill).Render(SkillWith(name, description));

        var map = ParseFrontmatter(rendered);

        // Adapters trim surrounding whitespace from display metadata by design; the YAML
        // encoder preserves everything else (proven by Scalar_AlwaysProducesParseableYaml).
        Assert.Equal(name.Trim(), map["name"]);
        Assert.Equal(description.Trim(), map["description"]);
    }

    [Theory]
    [MemberData(nameof(TrickyValues))]
    public void Cursor_FrontmatterParsesAndRoundTrips(string name, string description)
    {
        var rendered = new CursorAdapter().Render(SkillWith(name, description));

        var map = ParseFrontmatter(rendered);

        Assert.Equal(description.Trim(), map["description"]);
        Assert.Equal("false", map["alwaysApply"]?.ToString());
    }

    [Fact]
    public void GeneratedFrontmatter_WithColonHashQuotesNewline_ParsesWithYamlDotNet()
    {
        // A single value exercising all four hazardous characters at once.
        const string name = "Name: with \"colon\" # and\nnewline";
        const string description = "Desc: has \"quotes\" # hash\nsecond line";
        var skill = SkillWith(name, description);

        var skillFolder = ParseFrontmatter(new SkillFolderAdapter(TargetIds.ClaudeSkill).Render(skill));
        Assert.Equal(name, skillFolder["name"]);
        Assert.Equal(description, skillFolder["description"]);

        var cursor = ParseFrontmatter(new CursorAdapter().Render(skill));
        Assert.Equal(description, cursor["description"]);
        Assert.Equal("false", cursor["alwaysApply"]?.ToString());
    }

    [Theory]
    [InlineData("Plain Value", "Plain Value")]            // safe → unquoted
    [InlineData("has: colon", "\"has: colon\"")]          // quoted
    [InlineData("has # hash", "\"has # hash\"")]          // quoted
    [InlineData("quote \" here", "\"quote \\\" here\"")] // escaped quote
    [InlineData("line1\nline2", "\"line1\\nline2\"")]    // escaped newline
    public void Scalar_EncodesAsExpected(string input, string expected)
    {
        Assert.Equal(expected, Yaml.Scalar(input));
    }

    [Theory]
    [MemberData(nameof(TrickyValues))]
    public void Scalar_AlwaysProducesParseableYaml(string name, string description)
    {
        foreach (var value in new[] { name, description })
        {
            var doc = $"value: {Yaml.Scalar(value)}";
            var map = Parser.Deserialize<Dictionary<string, object>>(doc)!;
            Assert.Equal(value, map["value"]);
        }
    }
}
