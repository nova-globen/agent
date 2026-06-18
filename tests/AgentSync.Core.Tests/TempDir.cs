namespace AgentSync.Core.Tests;

/// <summary>Creates a unique temporary directory that is deleted on dispose.</summary>
public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "agentsync-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Creates a real Git repository in this directory and returns its path.</summary>
    public string AsGitRepo()
    {
        Directory.CreateDirectory(System.IO.Path.Combine(Path, ".git"));
        return Path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
