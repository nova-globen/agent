namespace AgentSync.Core.Tests;

public sealed class GitRepositoryTests
{
    [Fact]
    public void Discover_FindsRootFromNestedSubdirectory()
    {
        using var temp = new TempDir();
        temp.AsGitRepo();
        var nested = Path.Combine(temp.Path, "a", "b", "c");
        Directory.CreateDirectory(nested);

        var root = GitRepository.Discover(nested);

        Assert.Equal(Path.GetFullPath(temp.Path), root);
    }

    [Fact]
    public void Discover_DoesNotTreatDirectoryWithoutGitAsRoot()
    {
        // The temp dir has no .git, so it must not be reported as the repository root.
        // (We avoid asserting null because ancestors of the temp path may themselves
        // be Git repositories on the host running the tests.)
        using var temp = new TempDir();

        var root = GitRepository.Discover(temp.Path);

        Assert.NotEqual(Path.GetFullPath(temp.Path), root);
    }

    [Fact]
    public void Discover_HandlesGitFileForWorktrees()
    {
        using var temp = new TempDir();
        File.WriteAllText(Path.Combine(temp.Path, ".git"), "gitdir: /somewhere/else");

        var root = GitRepository.Discover(temp.Path);

        Assert.Equal(Path.GetFullPath(temp.Path), root);
    }
}
