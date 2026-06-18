using AgentSync.Core;
using AgentSync.Core.Drift;
using AgentSync.Core.Configuration;
using AgentSync.Core.Projections;

namespace AgentSync.Core.Tests.Drift;

public sealed class DriftDetectorTests
{
    private static void InitAndSync(string root)
    {
        new InitService(root).Run();
        new SyncService(root).Run();
    }

    [Fact]
    public void Detect_AfterSync_NoDrift()
    {
        using var temp = new TempDir();
        InitAndSync(temp.Path);

        var report = new DriftDetector(temp.Path).Detect();

        Assert.True(report.ConfigValid);
        Assert.False(report.HasDrift);
        Assert.All(report.Items, i => Assert.Equal(DriftKind.InSync, i.Kind));
    }

    [Fact]
    public void Detect_BeforeSync_AllMissing()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();

        var report = new DriftDetector(temp.Path).Detect();

        Assert.True(report.HasDrift);
        Assert.All(report.Items, i => Assert.Equal(DriftKind.Missing, i.Kind));
    }

    [Fact]
    public void Detect_InvalidConfig_ReportsConfigInvalid()
    {
        using var temp = new TempDir();

        var report = new DriftDetector(temp.Path).Detect();

        Assert.False(report.ConfigValid);
        Assert.True(report.HasDrift);
    }

    [Fact]
    public void Detect_OutdatedProjection_WhenCanonicalChanges()
    {
        using var temp = new TempDir();
        InitAndSync(temp.Path);
        // Change the canonical SKILL.md so the generated output should differ.
        var skillMd = Path.Combine(temp.Path, ".agent", "skills", "example-skill", "SKILL.md");
        File.WriteAllText(skillMd, "# Example Skill\n\nBrand new canonical content.\n");

        var report = new DriftDetector(temp.Path).Detect();

        Assert.True(report.HasDrift);
        Assert.Contains(report.Items, i => i.Kind == DriftKind.Outdated);
    }

    [Fact]
    public void Detect_ManualEdit_InSharedSection()
    {
        using var temp = new TempDir();
        InitAndSync(temp.Path);
        var agents = Path.Combine(temp.Path, "AGENTS.md");
        File.WriteAllText(agents, File.ReadAllText(agents).Replace("Describe what", "EDITED what"));

        var report = new DriftDetector(temp.Path).Detect();

        Assert.Contains(report.Items, i => i.TargetId == TargetIds.AgentsMd && i.Kind == DriftKind.ManualEdit);
    }

    [Fact]
    public void Detect_ManualEdit_InWholeFile()
    {
        using var temp = new TempDir();
        InitAndSync(temp.Path);
        var cursor = Path.Combine(temp.Path, ".cursor", "rules", "example-skill.mdc");
        File.WriteAllText(cursor, "totally different\n");

        var report = new DriftDetector(temp.Path).Detect();

        Assert.Contains(report.Items, i => i.TargetId == TargetIds.Cursor && i.Kind == DriftKind.ManualEdit);
    }

    [Fact]
    public void Detect_OrphanLockEntry_IsReported()
    {
        using var temp = new TempDir();
        InitAndSync(temp.Path);
        var lockPath = Path.Combine(temp.Path, ".agent", "lock.json");
        var lockfile = Lockfile.Load(lockPath);
        lockfile.Record("ghost-skill", TargetIds.AgentsMd, "AGENTS.md", "sha256:dead");
        lockfile.Save(lockPath);

        var report = new DriftDetector(temp.Path).Detect();

        Assert.True(report.HasDrift);
        Assert.Contains(report.Orphans, o => o.Skill == "ghost-skill");
    }

    [Fact]
    public void Detect_MissingLockEntry_FlaggedAsLockMismatch()
    {
        using var temp = new TempDir();
        InitAndSync(temp.Path);
        // Wipe the lockfile but leave generated files in place.
        File.WriteAllText(Path.Combine(temp.Path, ".agent", "lock.json"), "{\"version\":1,\"projections\":{}}");

        var report = new DriftDetector(temp.Path).Detect();

        Assert.True(report.HasDrift);
        Assert.Contains(report.Items, i => i.LockMismatch);
    }
}
