namespace AgentSync.Core;

/// <summary>Outcome of writing a single file during <c>agent init</c>.</summary>
public enum FileAction
{
    Created,
    Skipped,
    Overwritten,
}

public sealed record InitFileResult(string RelativePath, FileAction Action);

public sealed record InitResult(IReadOnlyList<InitFileResult> Files)
{
    public bool AnyCreated => Files.Any(f => f.Action is FileAction.Created or FileAction.Overwritten);
}

/// <summary>
/// Scaffolds the canonical <c>.agent/</c> structure and Git hooks.
/// </summary>
public sealed class InitService
{
    private readonly RepoLayout _layout;

    public InitService(string repoRoot) => _layout = new RepoLayout(repoRoot);

    public InitResult Run(bool force = false)
    {
        Directory.CreateDirectory(_layout.AgentDir);
        Directory.CreateDirectory(_layout.SkillsDir);
        Directory.CreateDirectory(_layout.HooksDir);

        var defaultSkillDir = Path.Combine(_layout.SkillsDir, Templates.DefaultSkillId);
        Directory.CreateDirectory(defaultSkillDir);

        var results = new List<InitFileResult>
        {
            WriteFile(_layout.ConfigFile, Templates.AgentYaml, force),
            WriteFile(_layout.LockFile, Templates.LockJson, force),
            WriteFile(Path.Combine(defaultSkillDir, "skill.yaml"), Templates.DefaultSkillYaml, force),
            WriteFile(Path.Combine(defaultSkillDir, "SKILL.md"), Templates.DefaultSkillMarkdown, force),
            WriteFile(_layout.PreCommitHook, Templates.PreCommitHook, force, executable: true),
            WriteFile(_layout.PrePushHook, Templates.PrePushHook, force, executable: true),
        };

        return new InitResult(results);
    }

    private InitFileResult WriteFile(string absolutePath, string content, bool force, bool executable = false)
    {
        var exists = File.Exists(absolutePath);
        if (exists && !force)
        {
            return new InitFileResult(_layout.Relative(absolutePath), FileAction.Skipped);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        File.WriteAllText(absolutePath, Normalize(content));

        if (executable)
        {
            MakeExecutable(absolutePath);
        }

        return new InitFileResult(_layout.Relative(absolutePath), exists ? FileAction.Overwritten : FileAction.Created);
    }

    private static string Normalize(string content)
    {
        // Use LF line endings; hooks are bash scripts and must run on Unix.
        var normalized = content.Replace("\r\n", "\n");
        return normalized.EndsWith('\n') ? normalized : normalized + "\n";
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
            // Best effort; permissions may not be settable on all filesystems.
        }
    }
}
