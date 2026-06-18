namespace AgentSync.Core.Tests;

public sealed class InitServiceTests
{
    [Fact]
    public void Run_CreatesCanonicalStructureAndHooks()
    {
        using var temp = new TempDir();

        var result = new InitService(temp.Path).Run();

        Assert.True(File.Exists(Path.Combine(temp.Path, ".agent", "agent.yaml")));
        Assert.True(File.Exists(Path.Combine(temp.Path, ".agent", "lock.json")));
        Assert.True(File.Exists(Path.Combine(temp.Path, ".agent", "skills", "example-skill", "skill.yaml")));
        Assert.True(File.Exists(Path.Combine(temp.Path, ".agent", "skills", "example-skill", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(temp.Path, ".githooks", "pre-commit")));
        Assert.True(File.Exists(Path.Combine(temp.Path, ".githooks", "pre-push")));

        Assert.All(result.Files, f => Assert.Equal(FileAction.Created, f.Action));
        Assert.True(result.AnyCreated);
    }

    [Fact]
    public void Run_DoesNotOverwriteExistingFilesWithoutForce()
    {
        using var temp = new TempDir();
        var configPath = Path.Combine(temp.Path, ".agent", "agent.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, "user: content");

        var result = new InitService(temp.Path).Run(force: false);

        Assert.Equal("user: content", File.ReadAllText(configPath));
        var configResult = result.Files.Single(f => f.RelativePath == ".agent/agent.yaml");
        Assert.Equal(FileAction.Skipped, configResult.Action);
    }

    [Fact]
    public void Run_WithForce_OverwritesExistingFiles()
    {
        using var temp = new TempDir();
        var configPath = Path.Combine(temp.Path, ".agent", "agent.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, "user: content");

        var result = new InitService(temp.Path).Run(force: true);

        Assert.Contains("version: 1", File.ReadAllText(configPath));
        var configResult = result.Files.Single(f => f.RelativePath == ".agent/agent.yaml");
        Assert.Equal(FileAction.Overwritten, configResult.Action);
    }

    [Fact]
    public void Run_HooksUseLfLineEndings()
    {
        using var temp = new TempDir();

        new InitService(temp.Path).Run();

        var hook = File.ReadAllText(Path.Combine(temp.Path, ".githooks", "pre-commit"));
        Assert.DoesNotContain("\r\n", hook);
        Assert.StartsWith("#!/usr/bin/env bash", hook);
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
        new InitService(temp.Path).Run();

        var mode = File.GetUnixFileMode(Path.Combine(temp.Path, ".githooks", "pre-commit"));
        Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
    }
}
