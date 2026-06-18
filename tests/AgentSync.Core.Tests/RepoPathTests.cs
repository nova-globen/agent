namespace AgentSync.Core.Tests;

public sealed class RepoPathTests
{
    [Theory]
    [InlineData("AGENTS.md")]
    [InlineData(".cursor/rules/skill.mdc")]
    [InlineData(".claude/skills/x/SKILL.md")]
    [InlineData("a/b/c.txt")]
    [InlineData("./AGENTS.md")]
    [InlineData("a/./b.txt")]
    [InlineData("a/b/../c.txt")] // stays within root
    public void IsSafeRelative_AcceptsValidRelativePaths(string path)
    {
        Assert.True(RepoPath.IsSafeRelative(path, out var error), error);
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("/AGENTS.md")]
    public void IsSafeRelative_RejectsUnixAbsolutePaths(string path)
    {
        Assert.False(RepoPath.IsSafeRelative(path, out _));
    }

    [Theory]
    [InlineData("../escape.md")]
    [InlineData("../../escape")]
    [InlineData("a/../../escape.txt")]
    [InlineData("..\\..\\escape")] // Windows-style traversal, normalized on all OSes
    public void IsSafeRelative_RejectsEscapingPaths(string path)
    {
        Assert.False(RepoPath.IsSafeRelative(path, out _));
    }

    [Theory]
    [InlineData("C:\\Windows\\system32")]
    [InlineData("C:/Windows/system32")]
    [InlineData("D:\\data\\x.md")]
    [InlineData("\\\\server\\share\\x")] // UNC
    [InlineData("//server/share/x")]
    public void IsSafeRelative_RejectsWindowsAbsoluteAndUncPaths(string path)
    {
        Assert.False(RepoPath.IsSafeRelative(path, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSafeRelative_RejectsEmptyPaths(string path)
    {
        Assert.False(RepoPath.IsSafeRelative(path, out _));
    }

    [Fact]
    public void Resolve_ReturnsPathWithinRoot()
    {
        using var temp = new TempDir();

        var resolved = RepoPath.Resolve(temp.Path, "a/b.txt");

        Assert.StartsWith(Path.GetFullPath(temp.Path), resolved);
        Assert.EndsWith("b.txt", resolved);
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("../../escape")]
    [InlineData("C:\\Windows")]
    public void Resolve_ThrowsForUnsafePaths(string path)
    {
        using var temp = new TempDir();

        Assert.Throws<RepoPathException>(() => RepoPath.Resolve(temp.Path, path));
    }
}
