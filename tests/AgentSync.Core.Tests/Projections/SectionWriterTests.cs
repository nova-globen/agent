using AgentSync.Core.Projections;

namespace AgentSync.Core.Tests.Projections;

public sealed class SectionWriterTests
{
    [Fact]
    public void Apply_CreatesFileWithSection()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "AGENTS.md");

        var result = SectionWriter.Apply(path, "s1", "agents_md", "Hello world");

        Assert.Equal(ProjectionChange.Created, result.Change);
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("Hello world", text);
        Assert.EndsWith("\n", text);
    }

    [Fact]
    public void Apply_SecondTimeSameContent_DoesNotRewrite()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "AGENTS.md");
        SectionWriter.Apply(path, "s1", "agents_md", "Hello");
        var firstWrite = File.GetLastWriteTimeUtc(path);

        var result = SectionWriter.Apply(path, "s1", "agents_md", "Hello");

        Assert.Equal(ProjectionChange.Unchanged, result.Change);
        Assert.Equal(firstWrite, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void Apply_PreservesUserContentOutsideMarkers()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "AGENTS.md");
        File.WriteAllText(path, "# Title\n\nHand-written notes.\n");

        SectionWriter.Apply(path, "s1", "agents_md", "Generated");

        var text = File.ReadAllText(path);
        Assert.Contains("Hand-written notes.", text);
        Assert.Contains("Generated", text);
    }

    [Fact]
    public void Apply_ManualEditInSection_NotOverwrittenWithoutForce()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "AGENTS.md");
        SectionWriter.Apply(path, "s1", "agents_md", "Generated body");
        // Tamper with the body inside the markers.
        var tampered = File.ReadAllText(path).Replace("Generated body", "Edited by hand");
        File.WriteAllText(path, tampered);

        var result = SectionWriter.Apply(path, "s1", "agents_md", "Regenerated body");

        Assert.Equal(ProjectionChange.SkippedManualEdit, result.Change);
        Assert.Contains("Edited by hand", File.ReadAllText(path));
    }
}
