namespace AgentSync.Core.Sessions.Providers;

/// <summary>
/// Base for agents whose on-disk layout is not a simple function of the project path (the
/// store is keyed by an opaque id or hash, or is a flat history). Sessions are found by
/// scanning candidate files under the provider root and matching any absolute-path spelling
/// of the project directory inside their text. On restore the file is written back to the
/// same relative location and its embedded paths are rewritten for the destination.
/// </summary>
public abstract class ContentMatchSessionProvider : ISessionProvider
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public virtual IReadOnlyList<string> Aliases => Array.Empty<string>();
    public virtual bool Experimental => true;

    public abstract string Root(SessionEnvironment env);

    /// <summary>Sub-directories of <see cref="Root"/> to scan (relative). Empty = the root itself.</summary>
    protected abstract IReadOnlyList<string> ScanSubdirectories { get; }

    /// <summary>File extensions (lowercase, with dot) considered session content.</summary>
    protected virtual IReadOnlyList<string> Extensions => new[] { ".json", ".jsonl", ".log", ".md", ".txt" };

    public SessionCollection Collect(SessionEnvironment env, string projectPath)
    {
        var root = Root(env);
        var entries = new List<SessionEntry>();
        if (!Directory.Exists(root))
        {
            return SessionCollection.Empty(Id);
        }

        var needles = ProjectPathVariants(projectPath, env.Platform);

        foreach (var sub in ScanSubdirectories.Count == 0 ? new[] { string.Empty } : ScanSubdirectories.ToArray())
        {
            var dir = string.IsNullOrEmpty(sub) ? root : Path.Combine(root, sub);
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                         .OrderBy(f => f, StringComparer.Ordinal))
            {
                if (!Extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                {
                    continue;
                }

                if (FileMentions(file, needles, env.Platform))
                {
                    var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                    entries.Add(new SessionEntry(rel, file));
                }
            }
        }

        return new SessionCollection(Id, entries);
    }

    public virtual RestorePlacement? Place(
        SessionManifest manifest,
        SessionEnvironment destEnv,
        string destProjectPath,
        string archivePath)
    {
        var dest = Path.Combine(Root(destEnv), archivePath.Replace('/', Path.DirectorySeparatorChar));
        return new RestorePlacement(dest, RewriteText: true);
    }

    /// <summary>All absolute-path spellings of a project directory to search for.</summary>
    internal static IReadOnlyList<string> ProjectPathVariants(string projectPath, SessionPlatform platform)
    {
        var set = new List<string> { projectPath };
        var loc = LocationPath.Parse(projectPath);
        if (loc is not null)
        {
            foreach (var style in new[] { PathStyle.Windows, PathStyle.WindowsForward, PathStyle.Wsl, PathStyle.Unix })
            {
                var r = loc.Render(style);
                if (r is not null)
                {
                    set.Add(r);
                    if (style == PathStyle.Windows)
                    {
                        set.Add(r.Replace("\\", "\\\\"));
                    }
                }
            }
        }

        return set.Where(s => s.Length > 0).Distinct().ToList();
    }

    private static bool FileMentions(string file, IReadOnlyList<string> needles, SessionPlatform platform)
    {
        string text;
        try
        {
            var info = new FileInfo(file);
            if (info.Length > 64 * 1024 * 1024)
            {
                return false; // Skip implausibly large files to bound the scan.
            }

            text = File.ReadAllText(file);
        }
        catch
        {
            return false;
        }

        var comparison = platform == SessionPlatform.Windows
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return needles.Any(n => text.Contains(n, comparison));
    }
}
