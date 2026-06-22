using System.Text.Json;

namespace AgentSync.Core.Sessions.Providers;

/// <summary>
/// Claude Code stores one folder per project under <c>~/.claude/projects/&lt;encoded-cwd&gt;/</c>,
/// where the folder name is the absolute working directory with path separators replaced by
/// dashes, and session transcripts are <c>&lt;uuid&gt;.jsonl</c> files (with sidecar
/// sub-directories). The encoding is lossy, so backup first tries the encoded folder and then
/// falls back to scanning every project folder and matching the <c>cwd</c> recorded inside the
/// transcripts. Restore recomputes the folder name for the destination project path.
/// </summary>
public sealed class ClaudeSessionProvider : ISessionProvider
{
    public string Id => "claude";
    public string DisplayName => "Claude Code";
    public IReadOnlyList<string> Aliases => new[] { "claude-code", "claudecode" };
    public bool Experimental => false;

    public string Root(SessionEnvironment env)
    {
        var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        return string.IsNullOrEmpty(configDir)
            ? Path.Combine(env.HomeDirectory, ".claude")
            : configDir;
    }

    public SessionCollection Collect(SessionEnvironment env, string projectPath)
    {
        var projectsDir = Path.Combine(Root(env), "projects");
        var key = SessionProviderSupport.EncodePathKey(projectPath);
        var dir = Path.Combine(projectsDir, key);

        if (!Directory.Exists(dir))
        {
            // Fall back to matching the recorded cwd, which sidesteps the lossy folder encoding.
            var matched = FindByRecordedCwd(projectsDir, projectPath, env.Platform);
            if (matched is not null)
            {
                key = Path.GetFileName(matched);
                dir = matched;
            }
        }

        var entries = SessionProviderSupport.EnumerateRelative(dir, archivePrefix: string.Empty).ToList();
        return new SessionCollection(key, entries);
    }

    public RestorePlacement? Place(
        SessionManifest manifest,
        SessionEnvironment destEnv,
        string destProjectPath,
        string archivePath)
    {
        var newKey = SessionProviderSupport.EncodePathKey(destProjectPath);
        var relative = archivePath.Replace('/', Path.DirectorySeparatorChar);
        var dest = Path.Combine(Root(destEnv), "projects", newKey, relative);
        var rewrite = archivePath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)
            || archivePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        return new RestorePlacement(dest, rewrite);
    }

    private static string? FindByRecordedCwd(string projectsDir, string projectPath, SessionPlatform platform)
    {
        if (!Directory.Exists(projectsDir))
        {
            return null;
        }

        foreach (var dir in Directory.EnumerateDirectories(projectsDir).OrderBy(d => d, StringComparer.Ordinal))
        {
            var transcript = Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.Ordinal)
                .FirstOrDefault();
            if (transcript is null)
            {
                continue;
            }

            var cwd = ReadTranscriptCwd(transcript);
            if (cwd is not null && SessionProviderSupport.PathsEqual(cwd, projectPath, platform))
            {
                return dir;
            }
        }

        return null;
    }

    /// <summary>Reads the <c>cwd</c> field from a Claude transcript (the first line may lack it).</summary>
    internal static string? ReadTranscriptCwd(string file)
    {
        try
        {
            using var reader = new StreamReader(file);
            for (var i = 0; i < 12; i++)
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                if (line.Length == 0 || line[0] != '{')
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("cwd", out var cwd) && cwd.ValueKind == JsonValueKind.String)
                    {
                        var value = cwd.GetString();
                        if (!string.IsNullOrEmpty(value))
                        {
                            return value;
                        }
                    }
                }
                catch (JsonException)
                {
                    // Skip non-JSON lines.
                }
            }
        }
        catch
        {
            // Unreadable file.
        }

        return null;
    }
}
