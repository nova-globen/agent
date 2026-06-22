namespace AgentSync.Core.Sessions;

/// <summary>One session file to archive: its path inside the archive and its source on disk.</summary>
public sealed record SessionEntry(string ArchivePath, string AbsolutePath);

/// <summary>The set of session files found for a project, plus a provider-specific store key.</summary>
public sealed record SessionCollection(string StoreKey, IReadOnlyList<SessionEntry> Entries)
{
    public static SessionCollection Empty(string storeKey)
        => new(storeKey, Array.Empty<SessionEntry>());
}

/// <summary>
/// Where an archived file should be written on restore, and whether its text content should
/// be path-rewritten for the destination environment.
/// </summary>
public sealed record RestorePlacement(string AbsoluteDestPath, bool RewriteText);

/// <summary>
/// Knows how a single AI agent stores its per-project session history: how to find a
/// project's sessions for backup, and where each archived file belongs on restore (which
/// may be a different machine, OS, or project path).
/// </summary>
public interface ISessionProvider
{
    /// <summary>Stable id used on the command line, e.g. <c>claude</c>.</summary>
    string Id { get; }

    /// <summary>Human-readable name, e.g. <c>Claude Code</c>.</summary>
    string DisplayName { get; }

    /// <summary>Alternate names accepted on the command line.</summary>
    IReadOnlyList<string> Aliases { get; }

    /// <summary>
    /// True when this provider's layout is well-established and exercised; experimental
    /// providers still work but warn that the on-disk layout may be incomplete.
    /// </summary>
    bool Experimental { get; }

    /// <summary>The provider's base directory under the given environment (may not exist).</summary>
    string Root(SessionEnvironment env);

    /// <summary>Collects the session files belonging to <paramref name="projectPath"/>.</summary>
    SessionCollection Collect(SessionEnvironment env, string projectPath);

    /// <summary>
    /// Computes the absolute destination for an archived file on restore, or <c>null</c> to
    /// skip it. <paramref name="archivePath"/> is relative to the archive's files root.
    /// </summary>
    RestorePlacement? Place(
        SessionManifest manifest,
        SessionEnvironment destEnv,
        string destProjectPath,
        string archivePath);
}

/// <summary>Shared helpers for session providers.</summary>
public static class SessionProviderSupport
{
    /// <summary>
    /// Encodes an absolute project path into the single-segment folder name Claude Code uses
    /// under <c>~/.claude/projects</c>: path separators (<c>/</c>, <c>\</c>) and the drive
    /// colon become dashes, while letters, digits, <c>_</c>, <c>.</c>, and existing dashes are
    /// preserved. Self-consistent across platforms (a Windows <c>C:\x</c> and a WSL
    /// <c>/mnt/c/x</c> each encode deterministically), so restore can recompute the key for the
    /// new path. The encoding is lossy, so backup also falls back to matching by the recorded
    /// working directory inside the session files.
    /// </summary>
    public static string EncodePathKey(string path)
    {
        var chars = new char[path.Length];
        for (var i = 0; i < path.Length; i++)
        {
            var c = path[i];
            var keep = (c is >= 'a' and <= 'z') || (c is >= 'A' and <= 'Z') || (c is >= '0' and <= '9')
                || c is '_' or '.' or '-';
            chars[i] = keep ? c : '-';
        }

        return new string(chars);
    }

    /// <summary>Normalises a path for equality comparison (trailing separators, case on Windows).</summary>
    public static bool PathsEqual(string a, string b, SessionPlatform platform)
    {
        static string Norm(string p) => p.Replace('\\', '/').TrimEnd('/');
        var comparison = platform == SessionPlatform.Windows
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Norm(a), Norm(b), comparison);
    }

    /// <summary>Enumerates files under <paramref name="dir"/> with archive-relative paths.</summary>
    public static IEnumerable<SessionEntry> EnumerateRelative(string dir, string archivePrefix)
    {
        if (!Directory.Exists(dir))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            var archivePath = string.IsNullOrEmpty(archivePrefix) ? rel : $"{archivePrefix}/{rel}";
            yield return new SessionEntry(archivePath, file);
        }
    }
}
