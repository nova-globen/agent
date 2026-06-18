using AgentSync.Core.Configuration;

namespace AgentSync.Core.Tests.Configuration;

public sealed class WorkspaceLoaderTests
{
    private static void WriteSkill(string repoRoot, string dirName, string id)
    {
        var dir = Path.Combine(repoRoot, ".agent", "skills", dirName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "skill.yaml"),
            $"id: {id}\nname: {id}\ndescription: desc\nversion: 0.1.0\ntargets:\n  agents_md:\n    enabled: true\n");
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), "# Body\n\nContent.\n");
    }

    [Fact]
    public void Load_FreshlyInitializedRepo_IsValid()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();

        var ws = WorkspaceLoader.Load(temp.Path);

        Assert.True(ws.IsValid);
        Assert.NotNull(ws.Config);
        Assert.Single(ws.Skills);
    }

    [Fact]
    public void Load_MissingConfig_ReportsError()
    {
        using var temp = new TempDir();

        var ws = WorkspaceLoader.Load(temp.Path);

        Assert.False(ws.IsValid);
        Assert.Null(ws.Config);
        Assert.Contains(ws.Validation.Messages, m => m.Code == "config.missing");
    }

    [Fact]
    public void Load_SkillMissingManifest_ReportsError()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        Directory.CreateDirectory(Path.Combine(temp.Path, ".agent", "skills", "orphan"));

        var ws = WorkspaceLoader.Load(temp.Path);

        Assert.Contains(ws.Validation.Messages, m => m.Code == "skill.manifest-missing");
    }

    [Fact]
    public void Load_DuplicateSkillIds_ReportsError()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        // Remove the default example skill to control the set precisely.
        Directory.Delete(Path.Combine(temp.Path, ".agent", "skills", "example-skill"), recursive: true);
        WriteSkill(temp.Path, "first", "shared-id");
        WriteSkill(temp.Path, "second", "shared-id");

        var ws = WorkspaceLoader.Load(temp.Path);

        // Both directories share an id; one of them must be reported as a duplicate.
        Assert.Contains(ws.Validation.Messages, m => m.Code == "skill.duplicate-id");
        // The folder/id mismatch also fires (folder != id), so just confirm invalidity.
        Assert.False(ws.IsValid);
    }

    [Fact]
    public void Load_MalformedConfig_ReportsParseError()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        File.WriteAllText(Path.Combine(temp.Path, ".agent", "agent.yaml"), "version: 1\n  : : bad");

        var ws = WorkspaceLoader.Load(temp.Path);

        Assert.Contains(ws.Validation.Messages, m => m.Code == "config.parse-error");
    }
}
