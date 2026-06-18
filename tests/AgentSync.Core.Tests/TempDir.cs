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

    /// <summary>Creates a fake <c>.git</c> directory (enough for repo-root discovery).</summary>
    public string AsGitRepo()
    {
        Directory.CreateDirectory(System.IO.Path.Combine(Path, ".git"));
        return Path;
    }

    /// <summary>Runs <c>git init</c> to create a real repository. Returns false if git is unavailable.</summary>
    public bool InitRealGitRepo()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git")
            {
                WorkingDirectory = Path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("init");
            psi.ArgumentList.Add("-q");
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null)
            {
                return false;
            }

            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public string? GetGitConfig(string key)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git")
            {
                WorkingDirectory = Path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("config");
            psi.ArgumentList.Add("--get");
            psi.ArgumentList.Add(key);
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null)
            {
                return null;
            }

            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return p.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch
        {
            return null;
        }
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
