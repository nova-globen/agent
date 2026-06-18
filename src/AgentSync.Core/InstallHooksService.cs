namespace AgentSync.Core;

public sealed record HookStatus(string Name, bool Present, bool Executable);

public sealed record InstallHooksResult(
    bool GitConfigured,
    string HooksPath,
    IReadOnlyList<HookStatus> Hooks,
    string? Error)
{
    public bool Success => GitConfigured && Error is null && Hooks.All(h => h.Present);
}

/// <summary>
/// Wires Git to use <c>.githooks</c> (via <c>core.hooksPath</c>), writes the hook
/// scripts if missing, and makes them executable on Unix-like systems.
/// </summary>
public sealed class InstallHooksService
{
    private readonly RepoLayout _layout;

    public InstallHooksService(string repoRoot) => _layout = new RepoLayout(repoRoot);

    public InstallHooksResult Run()
    {
        Directory.CreateDirectory(_layout.HooksDir);
        EnsureHook(_layout.PreCommitHook, Templates.PreCommitHook);
        EnsureHook(_layout.PrePushHook, Templates.PrePushHook);

        var configured = GitRepository.SetHooksPath(_layout.RepoRoot, RepoLayout.DefaultHooksPath);
        var error = configured
            ? null
            : "Could not set core.hooksPath. Is Git installed and is this a Git repository?";

        var hooks = new[]
        {
            DescribeHook(_layout.PreCommitHook),
            DescribeHook(_layout.PrePushHook),
        };

        return new InstallHooksResult(configured, RepoLayout.DefaultHooksPath, hooks, error);
    }

    private static void EnsureHook(string path, string content)
    {
        if (!File.Exists(path))
        {
            var normalized = content.Replace("\r\n", "\n");
            if (!normalized.EndsWith('\n'))
            {
                normalized += "\n";
            }

            File.WriteAllText(path, normalized);
        }

        MakeExecutable(path);
    }

    private HookStatus DescribeHook(string path)
    {
        var present = File.Exists(path);
        var executable = present && IsExecutable(path);
        return new HookStatus(_layout.Relative(path), present, executable);
    }

    private static bool IsExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return true; // execute bits are not meaningful on Windows
        }

        try
        {
            return File.GetUnixFileMode(path).HasFlag(UnixFileMode.UserExecute);
        }
        catch
        {
            return false;
        }
    }

    private static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            File.SetUnixFileMode(path, mode
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherExecute);
        }
        catch
        {
            // Best effort.
        }
    }
}
