using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;
using AgentSync.Core.Drift;
using AgentSync.Core.Projections;

namespace AgentSync.Core.Tests.Adapters;

public sealed class ProjectionApplierTests
{
    [Fact]
    public void ApplyAll_CreatesFilesAndRecordsLock()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        var ws = WorkspaceLoader.Load(temp.Path);
        var plan = new ProjectionPlanner().Plan(ws);
        var lockfile = new Lockfile();

        var outcomes = new ProjectionApplier(temp.Path).ApplyAll(plan, lockfile);

        Assert.All(outcomes, o => Assert.Equal(ProjectionChange.Created, o.Change));
        Assert.True(File.Exists(Path.Combine(temp.Path, "AGENTS.md")));
        Assert.True(File.Exists(Path.Combine(temp.Path, ".cursor", "rules", "code-review.mdc")));
        Assert.Equal(plan.Count, lockfile.Projections.Count);
    }

    [Fact]
    public void ApplyAll_SecondRun_IsUnchanged()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        var ws = WorkspaceLoader.Load(temp.Path);
        var plan = new ProjectionPlanner().Plan(ws);
        var lockfile = new Lockfile();
        var applier = new ProjectionApplier(temp.Path);

        applier.ApplyAll(plan, lockfile);
        var second = applier.ApplyAll(plan, lockfile);

        Assert.All(second, o => Assert.Equal(ProjectionChange.Unchanged, o.Change));
    }

    [Fact]
    public void Apply_WholeFile_ManualEdit_SkippedWithoutForce()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        var ws = WorkspaceLoader.Load(temp.Path);
        var plan = new ProjectionPlanner().Plan(ws);
        var lockfile = new Lockfile();
        var applier = new ProjectionApplier(temp.Path);
        applier.ApplyAll(plan, lockfile);

        // Tamper with a whole-file (cursor) projection.
        var cursorPath = Path.Combine(temp.Path, ".cursor", "rules", "code-review.mdc");
        File.WriteAllText(cursorPath, "hand edited\n");
        var cursorProj = plan.First(p => p.TargetId == TargetIds.Cursor);

        var skipped = applier.Apply(cursorProj, lockfile, force: false);
        Assert.Equal(ProjectionChange.SkippedManualEdit, skipped.Change);
        Assert.Equal("hand edited\n", File.ReadAllText(cursorPath));

        var forced = applier.Apply(cursorProj, lockfile, force: true);
        Assert.Equal(ProjectionChange.Updated, forced.Change);
        Assert.Contains("# Code Review", File.ReadAllText(cursorPath));
    }

    [Fact]
    public void Apply_DryRun_DoesNotWriteOrRecord()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        var ws = WorkspaceLoader.Load(temp.Path);
        var plan = new ProjectionPlanner().Plan(ws);
        var lockfile = new Lockfile();

        var outcomes = new ProjectionApplier(temp.Path).ApplyAll(plan, lockfile, force: false, dryRun: true);

        Assert.All(outcomes, o => Assert.Equal(ProjectionChange.Created, o.Change));
        Assert.False(File.Exists(Path.Combine(temp.Path, "AGENTS.md")));
        Assert.Empty(lockfile.Projections);
    }

    [Fact]
    public void Apply_SharedSection_ManualEdit_SkippedWithoutForce()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        var ws = WorkspaceLoader.Load(temp.Path);
        var plan = new ProjectionPlanner().Plan(ws);
        var lockfile = new Lockfile();
        var applier = new ProjectionApplier(temp.Path);
        applier.ApplyAll(plan, lockfile);

        var agentsPath = Path.Combine(temp.Path, "AGENTS.md");
        var tampered = File.ReadAllText(agentsPath).Replace("Reviews changes", "HAND EDITED skill");
        File.WriteAllText(agentsPath, tampered);

        var proj = plan.First(p => p.TargetId == TargetIds.AgentsMd);
        var outcome = applier.Apply(proj, lockfile, force: false);

        Assert.Equal(ProjectionChange.SkippedManualEdit, outcome.Change);
        Assert.Contains("HAND EDITED skill", File.ReadAllText(agentsPath));
    }

    // --- references/ asset projection ---

    [Fact]
    public void Apply_WholeFile_WithReferences_CopiesAssets()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();

        // Add a references/ directory to the canonical claude_skill.
        var refsDir = Path.Combine(temp.Path, ".agent", "skills", "code-review", "references");
        Directory.CreateDirectory(refsDir);
        File.WriteAllText(Path.Combine(refsDir, "checklist.md"), "## Checklist\n\n- Item one\n");

        var ws = WorkspaceLoader.Load(temp.Path);
        var plan = new ProjectionPlanner().Plan(ws);
        var lockfile = new Lockfile();
        var applier = new ProjectionApplier(temp.Path);

        applier.ApplyAll(plan, lockfile);

        var projectedRefs = Path.Combine(temp.Path, ".claude", "skills", "code-review", "references", "checklist.md");
        Assert.True(File.Exists(projectedRefs), "references/checklist.md should be projected alongside SKILL.md");
        Assert.Contains("Item one", File.ReadAllText(projectedRefs));
    }

    [Fact]
    public void Apply_WholeFile_WithReferences_DriftDetectedOnReferenceChange()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();

        var refsDir = Path.Combine(temp.Path, ".agent", "skills", "code-review", "references");
        Directory.CreateDirectory(refsDir);
        File.WriteAllText(Path.Combine(refsDir, "guide.md"), "Old content\n");

        var ws = WorkspaceLoader.Load(temp.Path);
        var plan = new ProjectionPlanner().Plan(ws);
        var lockfile = new Lockfile();
        var applier = new ProjectionApplier(temp.Path);
        applier.ApplyAll(plan, lockfile);
        lockfile.Save(Path.Combine(temp.Path, ".agent", "lock.json"));

        // Change the canonical reference file.
        File.WriteAllText(Path.Combine(refsDir, "guide.md"), "Updated content\n");

        var ws2 = WorkspaceLoader.Load(temp.Path);
        var plan2 = new ProjectionPlanner().Plan(ws2);
        var lockfile2 = Lockfile.Load(Path.Combine(temp.Path, ".agent", "lock.json"));

        // The claude_skill projection should show as Outdated (reference changed, SKILL.md same).
        var report = new DriftDetector(temp.Path).Detect();
        Assert.Contains(report.Items, i => i.TargetId == TargetIds.ClaudeSkill && i.Kind == DriftKind.Outdated);
    }
}
