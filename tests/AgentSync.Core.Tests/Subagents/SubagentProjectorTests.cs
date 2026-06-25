using AgentSync.Core;
using AgentSync.Core.Configuration;
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

    private static void WriteAgentYamlWithTomlTarget(TempDir dir, string tomlPath)
    {
        var agentDir = Path.Combine(dir.Path, ".agent");
        Directory.CreateDirectory(agentDir);
        File.WriteAllText(Path.Combine(agentDir, "agent.yaml"),
            $"version: 1\ntargets:\n  toml_agent:\n    enabled: true\n    path: {tomlPath}\n");
    }

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

    // --- toml_agent target ---

    [Fact]
    public void TomlAgent_Render_ProducesValidToml()
    {
        using var dir = new TempDir();
        var layout = Layout(dir);
        AddAgent(layout, "reviewer", "Reviews diffs.", "sonnet", "Read", "Grep");

        var agent = SubagentFiles.LoadAll(layout)[0];
        var toml = TomlAgentRenderer.Render(agent);

        Assert.Contains("name = 'reviewer'", toml);
        Assert.Contains("description = 'Reviews diffs.'", toml);
        Assert.Contains("model = 'sonnet'", toml);
        Assert.Contains("tools = ['Read', 'Grep']", toml);
        Assert.Contains("system_prompt", toml);
        Assert.Contains("Do the thing.", toml);
    }

    [Fact]
    public void Sync_TomlTarget_CreatesTomlFile()
    {
        using var dir = new TempDir();
        var layout = Layout(dir);
        WriteAgentYamlWithTomlTarget(dir, "codex/agents");
        AddAgent(layout, "helper", "Helps with tasks.");

        var report = new SubagentProjector(dir.Path).Sync(force: false, dryRun: false);

        // Two outcomes: claude_agent + toml_agent.
        Assert.Equal(2, report.Outcomes.Count);
        Assert.All(report.Outcomes, o => Assert.Equal(SubagentChange.Created, o.Change));

        var tomlPath = Path.Combine(dir.Path, "codex", "agents", "helper.toml");
        Assert.True(File.Exists(tomlPath));
        var content = File.ReadAllText(tomlPath);
        Assert.Contains("name = 'helper'", content);
        Assert.Contains("system_prompt", content);
    }

    [Fact]
    public void Sync_TomlTarget_Idempotent()
    {
        using var dir = new TempDir();
        var layout = Layout(dir);
        WriteAgentYamlWithTomlTarget(dir, "codex/agents");
        AddAgent(layout, "helper", "Helps.");

        var projector = new SubagentProjector(dir.Path);
        projector.Sync(false, false);
        var second = projector.Sync(false, false);

        Assert.All(second.Outcomes, o => Assert.Equal(SubagentChange.UpToDate, o.Change));
        Assert.Empty(projector.Detect());
    }

    [Fact]
    public void Detect_TomlTarget_MissingProjection()
    {
        using var dir = new TempDir();
        var layout = Layout(dir);
        WriteAgentYamlWithTomlTarget(dir, "codex/agents");
        AddAgent(layout, "helper", "Helps.");

        var drift = new SubagentProjector(dir.Path).Detect();

        Assert.Equal(2, drift.Count); // claude_agent + toml_agent both missing
        Assert.All(drift, d => Assert.Equal(SubagentDriftKind.Missing, d.Kind));
    }
}
