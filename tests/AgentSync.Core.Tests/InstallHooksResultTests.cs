namespace AgentSync.Core.Tests;

public sealed class InstallHooksResultTests
{
    private static InstallHooksResult Make(bool gitConfigured, string? error, params HookStatus[] hooks)
        => new(gitConfigured, ".githooks", hooks, error);

    [Fact]
    public void Success_True_WhenConfiguredPresentAndExecutable()
    {
        var result = Make(true, null,
            new HookStatus(".githooks/pre-commit", Present: true, Executable: true),
            new HookStatus(".githooks/pre-push", Present: true, Executable: true));

        Assert.True(result.Success);
    }

    [Fact]
    public void Success_False_WhenNotConfigured()
    {
        var result = Make(false, "boom",
            new HookStatus(".githooks/pre-commit", true, true),
            new HookStatus(".githooks/pre-push", true, true));

        Assert.False(result.Success);
    }

    [Fact]
    public void Success_False_WhenHookMissing()
    {
        var result = Make(true, null,
            new HookStatus(".githooks/pre-commit", Present: false, Executable: false),
            new HookStatus(".githooks/pre-push", Present: true, Executable: true));

        Assert.False(result.Success);
    }

    [Fact]
    public void Success_RequiresExecutableOnUnix_NotOnWindows()
    {
        var result = Make(true, null,
            new HookStatus(".githooks/pre-commit", Present: true, Executable: false),
            new HookStatus(".githooks/pre-push", Present: true, Executable: false));

        if (OperatingSystem.IsWindows())
        {
            Assert.True(result.Success); // execute bit not meaningful on Windows
        }
        else
        {
            Assert.False(result.Success); // present but not executable → failure on Unix
        }
    }
}
