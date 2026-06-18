namespace AgentSync.Core.Tests;

public sealed class InstallHooksServiceTests
{
    [Fact]
    public void Run_InRealGitRepo_ConfiguresHooksPathAndScripts()
    {
        using var temp = new TempDir();
        if (!temp.InitRealGitRepo())
        {
            return; // git not available in this environment
        }

        var result = new InstallHooksService(temp.Path).Run();

        Assert.True(result.GitConfigured);
        Assert.True(result.Success);
        Assert.Equal(".githooks", result.HooksPath);
        Assert.Equal(".githooks", temp.GetGitConfig("core.hooksPath"));
        Assert.True(File.Exists(Path.Combine(temp.Path, ".githooks", "pre-commit")));
        Assert.True(File.Exists(Path.Combine(temp.Path, ".githooks", "pre-push")));
    }

    [Fact]
    public void Run_WritesHookScriptsWhenMissing()
    {
        using var temp = new TempDir();
        if (!temp.InitRealGitRepo())
        {
            return;
        }

        var result = new InstallHooksService(temp.Path).Run();

        Assert.All(result.Hooks, h => Assert.True(h.Present));
        var hook = File.ReadAllText(Path.Combine(temp.Path, ".githooks", "pre-commit"));
        Assert.Contains("Agent Sync is required for this repository.", hook);
    }

    [Fact]
    public void Run_MakesHooksExecutableOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var temp = new TempDir();
        if (!temp.InitRealGitRepo())
        {
            return;
        }

        new InstallHooksService(temp.Path).Run();

        var mode = File.GetUnixFileMode(Path.Combine(temp.Path, ".githooks", "pre-commit"));
        Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
    }
}
