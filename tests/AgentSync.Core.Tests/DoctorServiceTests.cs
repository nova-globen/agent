namespace AgentSync.Core.Tests;

public sealed class DoctorServiceTests
{
    [Fact]
    public void Run_NotInRepo_ReportsGitCheckFailureAndStops()
    {
        var report = new DoctorService().Run(new DoctorInput(RepoRoot: null, HooksPath: null, AgentOnPath: true));

        Assert.False(report.AllOk);
        var git = report.Checks.Single(c => c.Name == "Git repository");
        Assert.False(git.Ok);
        // No config/lock/hook checks when not in a repo.
        Assert.DoesNotContain(report.Checks, c => c.Name == "Configuration");
    }

    [Fact]
    public void Run_FullyConfigured_AllChecksPass()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();

        var report = new DoctorService().Run(new DoctorInput(
            RepoRoot: temp.Path,
            HooksPath: ".githooks",
            AgentOnPath: true));

        Assert.True(report.AllOk);
        Assert.Contains(report.Checks, c => c.Name == "Git hooks" && c.Ok);
    }

    [Fact]
    public void Run_HooksPathNotConfigured_FlagsHooksCheck()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();

        var report = new DoctorService().Run(new DoctorInput(
            RepoRoot: temp.Path,
            HooksPath: null,
            AgentOnPath: true));

        var hooks = report.Checks.Single(c => c.Name == "Git hooks");
        Assert.False(hooks.Ok);
    }

    [Fact]
    public void Run_AgentNotOnPath_FlagsPathCheck()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();

        var report = new DoctorService().Run(new DoctorInput(
            RepoRoot: temp.Path,
            HooksPath: ".githooks",
            AgentOnPath: false));

        var path = report.Checks.Single(c => c.Name == "agent on PATH");
        Assert.False(path.Ok);
    }

    [Theory]
    [InlineData(".githooks")]
    [InlineData(".githooks/")]
    public void Run_AcceptsHooksPathWithTrailingSlash(string hooksPath)
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();

        var report = new DoctorService().Run(new DoctorInput(temp.Path, hooksPath, AgentOnPath: true));

        Assert.True(report.Checks.Single(c => c.Name == "Git hooks").Ok);
    }
}
