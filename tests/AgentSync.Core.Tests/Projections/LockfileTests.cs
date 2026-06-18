using AgentSync.Core.Projections;

namespace AgentSync.Core.Tests.Projections;

public sealed class LockfileTests
{
    [Fact]
    public void Parse_DefaultTemplate_HasEmptyProjections()
    {
        var lockfile = Lockfile.Parse(Templates.LockJson);

        Assert.Equal(1, lockfile.Version);
        Assert.Empty(lockfile.Projections);
    }

    [Fact]
    public void RecordAndGet_RoundTrips()
    {
        var lockfile = new Lockfile();
        lockfile.Record("code-review", "agents_md", "AGENTS.md", "sha256:abc");

        var entry = lockfile.Get("code-review", "agents_md");

        Assert.NotNull(entry);
        Assert.Equal("AGENTS.md", entry!.Path);
        Assert.Equal("sha256:abc", entry.Hash);
    }

    [Fact]
    public void Serialize_Then_Parse_PreservesEntries()
    {
        var lockfile = new Lockfile();
        lockfile.Record("s1", "agents_md", "AGENTS.md", "sha256:111");
        lockfile.Record("s2", "claude_md", "CLAUDE.md", "sha256:222");

        var roundTripped = Lockfile.Parse(lockfile.Serialize());

        Assert.Equal(2, roundTripped.Projections.Count);
        Assert.Equal("sha256:222", roundTripped.Get("s2", "claude_md")!.Hash);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsFromDisk()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, ".agent", "lock.json");
        var lockfile = new Lockfile();
        lockfile.Record("s1", "cursor", ".cursor/rules/s1.mdc", "sha256:deadbeef");
        lockfile.Save(path);

        var loaded = Lockfile.Load(path);

        Assert.Equal("sha256:deadbeef", loaded.Get("s1", "cursor")!.Hash);
    }

    [Fact]
    public void Record_SameKeyOverwrites()
    {
        var lockfile = new Lockfile();
        lockfile.Record("s1", "agents_md", "AGENTS.md", "sha256:old");
        lockfile.Record("s1", "agents_md", "AGENTS.md", "sha256:new");

        Assert.Single(lockfile.Projections);
        Assert.Equal("sha256:new", lockfile.Get("s1", "agents_md")!.Hash);
    }
}
