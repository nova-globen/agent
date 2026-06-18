namespace AgentSync.Core.Tests;

public sealed class StatusServiceTests
{
    [Fact]
    public void Run_NotInitialized_ReportsErrorAndProblems()
    {
        using var temp = new TempDir();

        var report = new StatusService(temp.Path).Run();

        Assert.False(report.Initialized);
        Assert.True(report.HasProblems);
        Assert.Contains(report.Issues, i => i.Code == "not-initialized" && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Run_AfterInitBeforeSync_ReportsMissingProjections()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();

        var report = new StatusService(temp.Path).Run();

        Assert.True(report.Initialized);
        Assert.Equal(1, report.SkillCount);
        // Projections have not been written yet, so drift is expected.
        Assert.True(report.HasProblems);
        Assert.Contains(report.Issues, i => i.Code == "drift-missing");
    }

    [Fact]
    public void Run_AfterInitAndSync_IsClean()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        new SyncService(temp.Path).Run();

        var report = new StatusService(temp.Path).Run();

        Assert.True(report.Initialized);
        Assert.False(report.HasProblems);
        Assert.Equal(1, report.SkillCount);
    }

    [Fact]
    public void Run_DetectsManualEditAfterSync()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        new SyncService(temp.Path).Run();
        var agents = Path.Combine(temp.Path, "AGENTS.md");
        File.WriteAllText(agents, File.ReadAllText(agents).Replace("Reviews changes", "HAND EDIT what"));

        var report = new StatusService(temp.Path).Run();

        Assert.True(report.HasProblems);
        Assert.Contains(report.Issues, i => i.Code == "drift-manual-edit");
    }

    [Fact]
    public void Run_MissingLockfile_ReportsError()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        File.Delete(Path.Combine(temp.Path, ".agent", "lock.json"));

        var report = new StatusService(temp.Path).Run();

        Assert.True(report.HasProblems);
        Assert.Contains(report.Issues, i => i.Code == "missing-lockfile");
    }

    [Fact]
    public void Run_NoSkills_WarnsButIsNotAProblem()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        Directory.Delete(Path.Combine(temp.Path, ".agent", "skills", "code-review"), recursive: true);

        var report = new StatusService(temp.Path).Run();

        Assert.Equal(0, report.SkillCount);
        Assert.False(report.HasProblems);
        Assert.Contains(report.Issues, i => i.Code == "no-skills" && i.Severity == IssueSeverity.Warning);
    }
}
