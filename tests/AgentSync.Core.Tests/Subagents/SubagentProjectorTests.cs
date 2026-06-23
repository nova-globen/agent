using AgentSync.Core;
using AgentSync.Core.Subagents;

namespace AgentSync.Core.Tests.Subagents;

public sealed class SubagentProjectorTests
{
    private static RepoLayout Layout(TempDir dir) => new(dir.Path);

    private static void AddAgent(RepoLayout layout, string id, string description, string? model = null, params string[] tools)
    {
        var yaml = SubagentFiles.RenderManifestYaml(id, id, description, model, color: null, tools);
        SubagentFiles.Write(layout, id, yaml, "## Role\n\nDo the thing.\n");
    }

    private static string ProjectionPath(TempDir dir, string id)
        => Path.Combine(dir.Path, ".claude", "agents", $"{id}.md");

    [Fact]
    public void Sync_CreatesProjectionWithFrontmatter()
    {
        using var dir = new TempDir();
        var layout = Layout(dir);
        AddAgent(layout, "reviewer", "Reviews diffs.", "sonnet", "Read", "Grep");

        var report = new SubagentProjector(dir.Path).Sync(force: false, dryRun: false);

        Assert.Single(report.Outcomes);
        Assert.Equal(SubagentChange.Created, report.Outcomes[0].Change);
        var content = File.ReadAllText(ProjectionPath(dir, "reviewer"));
        Assert.Contains("name: reviewer", content);
        Assert.Contains("description: Reviews diffs.", content);
        Assert.Contains("tools: Read, Grep", content);
        Assert.Contains("model: sonnet", content);
    }

    [Fact]
    public void Sync_Idempotent_SecondRunIsUpToDate()
    {
        using var dir = new TempDir();
        AddAgent(Layout(dir), "a", "desc");
        var projector = new SubagentProjector(dir.Path);

        projector.Sync(false, false);
        var second = projector.Sync(false, false);

        Assert.Equal(SubagentChange.UpToDate, second.Outcomes[0].Change);
        Assert.Empty(projector.Detect());
    }

    [Fact]
    public void Detect_MissingProjection()
    {
        using var dir = new TempDir();
        AddAgent(Layout(dir), "a", "desc");

        var drift = new SubagentProjector(dir.Path).Detect();

        Assert.Single(drift);
        Assert.Equal(SubagentDriftKind.Missing, drift[0].Kind);
    }

    [Fact]
    public void Detect_OutdatedAfterCanonicalEdit()
    {
        using var dir = new TempDir();
        var layout = Layout(dir);
        AddAgent(layout, "a", "desc");
        var projector = new SubagentProjector(dir.Path);
        projector.Sync(false, false);

        AddAgent(layout, "a", "a different description"); // canonical changed

        var drift = projector.Detect();
        Assert.Single(drift);
        Assert.Equal(SubagentDriftKind.Outdated, drift[0].Kind);
    }

    [Fact]
    public void ManualEdit_DetectedAndSkippedUnlessForce()
    {
        using var dir = new TempDir();
        AddAgent(Layout(dir), "a", "desc");
        var projector = new SubagentProjector(dir.Path);
        projector.Sync(false, false);

        File.AppendAllText(ProjectionPath(dir, "a"), "\nhand edit\n");

        Assert.Equal(SubagentDriftKind.ManualEdit, projector.Detect()[0].Kind);

        var skipped = projector.Sync(force: false, dryRun: false);
        Assert.True(skipped.AnySkippedManualEdits);

        var forced = projector.Sync(force: true, dryRun: false);
        Assert.Equal(SubagentChange.Updated, forced.Outcomes[0].Change);
        Assert.Empty(projector.Detect());
    }

    [Fact]
    public void Detect_OrphanWhenCanonicalRemoved()
    {
        using var dir = new TempDir();
        var layout = Layout(dir);
        AddAgent(layout, "a", "desc");
        var projector = new SubagentProjector(dir.Path);
        projector.Sync(false, false); // records lock entry

        Directory.Delete(SubagentFiles.AgentDir(layout, "a"), recursive: true);

        var drift = projector.Detect();
        Assert.Contains(drift, d => d.Kind == SubagentDriftKind.Orphan && d.Id == "a");
    }

    [Fact]
    public void DryRun_WritesNothing()
    {
        using var dir = new TempDir();
        AddAgent(Layout(dir), "a", "desc");

        var report = new SubagentProjector(dir.Path).Sync(force: false, dryRun: true);

        Assert.Equal(SubagentChange.Created, report.Outcomes[0].Change);
        Assert.False(File.Exists(ProjectionPath(dir, "a")));
    }

    [Fact]
    public void NoAgents_IsNoOp()
    {
        using var dir = new TempDir();
        var report = new SubagentProjector(dir.Path).Sync(false, false);
        Assert.Empty(report.Outcomes);
        Assert.Empty(new SubagentProjector(dir.Path).Detect());
    }
}
