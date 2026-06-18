using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class InstallHooksCommandTests
{
    [Fact]
    public void InstallHooks_OutsideGitRepo_ReturnsEnvironmentProblem()
    {
        using var h = new CliTestHarness();

        // Skip if the host has an ancestor Git repo above the temp dir (e.g. a stray
        // .git under the temp root), which would make this a valid repo.
        if (GitRepository.Discover(h.WorkingDirectory) is not null)
        {
            return;
        }

        var result = h.Invoke("install-hooks");

        Assert.Equal(ExitCodes.EnvironmentProblem, result.ExitCode);
        Assert.Contains("not inside a Git repository", result.StdErr);
    }

    [Fact]
    public void InstallHooks_InRealRepo_Succeeds()
    {
        using var h = new CliTestHarness();
        if (!h.MakeRealGitRepo())
        {
            return; // git unavailable
        }

        var result = h.Invoke("install-hooks");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("core.hooksPath set", result.StdOut);
        Assert.True(File.Exists(Path.Combine(h.WorkingDirectory, ".githooks", "pre-commit")));
    }

    [Fact]
    public void InstallHooks_UnknownOption_ReturnsInvalidUsage()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("install-hooks", "--nope");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }
}
