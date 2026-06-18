using System.Diagnostics;

namespace AgentSync.Core;

/// <summary>
/// Locates the Git repository root and reads Git configuration relevant to Agent Sync.
/// </summary>
public static class GitRepository
{
    /// <summary>
    /// Walks upward from <paramref name="startDirectory"/> looking for a <c>.git</c>
    /// directory or file. Returns the repository root, or <c>null</c> if none is found.
    /// </summary>
    public static string? Discover(string startDirectory)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (dir is not null)
        {
            var gitPath = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// Reads <c>core.hooksPath</c> for the repository at <paramref name="repoRoot"/>.
    /// Returns <c>null</c> if Git is unavailable or the value is unset.
    /// </summary>
    public static string? GetHooksPath(string repoRoot)
    {
        var result = RunGit(repoRoot, "config", "--get", "core.hooksPath");
        if (result is null)
        {
            return null;
        }

        var value = result.Trim();
        return value.Length == 0 ? null : value;
    }

    /// <summary>
    /// Runs <c>git config core.hooksPath &lt;value&gt;</c> in the repository.
    /// Returns <c>true</c> if Git ran successfully.
    /// </summary>
    public static bool SetHooksPath(string repoRoot, string value)
    {
        return RunGit(repoRoot, "config", "core.hooksPath", value) is not null;
    }

    private static string? RunGit(string repoRoot, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("git")
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            // Git not installed or not on PATH.
            return null;
        }
    }
}
