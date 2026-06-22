using System.Security.Cryptography;
using System.Text;

namespace AgentSync.Core.Sessions.Providers;

/// <summary>
/// Gemini CLI stores per-project temporary state (chat checkpoints, logs) under
/// <c>~/.gemini/tmp/&lt;id&gt;/</c>. Older versions named <c>&lt;id&gt;</c> with the SHA-256 of the
/// absolute project path; newer versions use a human-readable slug plus a <c>.project_root</c>
/// marker file holding the absolute path (and a <c>~/.gemini/projects.json</c> registry).
/// Backup matches a project by its <c>.project_root</c> marker, falling back to the legacy
/// SHA-256 directory. Restore writes the folder back under the same id and rewrites the
/// recorded path inside text files. Experimental.
/// </summary>
public sealed class GeminiSessionProvider : ISessionProvider
{
    private const string ProjectRootMarker = ".project_root";

    public string Id => "gemini";
    public string DisplayName => "Gemini CLI";
    public IReadOnlyList<string> Aliases => new[] { "google-gemini" };
    public bool Experimental => true;

    public string Root(SessionEnvironment env)
    {
        var overrideHome = Environment.GetEnvironmentVariable("GEMINI_CLI_HOME");
        return string.IsNullOrEmpty(overrideHome)
            ? Path.Combine(env.HomeDirectory, ".gemini")
            : Path.Combine(overrideHome, ".gemini");
    }

    public SessionCollection Collect(SessionEnvironment env, string projectPath)
    {
        var tmp = Path.Combine(Root(env), "tmp");
        var dir = LocateProjectDir(tmp, projectPath, env.Platform);
        if (dir is null)
        {
            return SessionCollection.Empty(LegacyHash(projectPath));
        }

        var id = Path.GetFileName(dir);
        var entries = SessionProviderSupport
            .EnumerateRelative(dir, archivePrefix: $"tmp/{id}")
            .ToList();
        return new SessionCollection(id, entries);
    }

    public RestorePlacement? Place(
        SessionManifest manifest,
        SessionEnvironment destEnv,
        string destProjectPath,
        string archivePath)
    {
        var dest = Path.Combine(Root(destEnv), archivePath.Replace('/', Path.DirectorySeparatorChar));
        var name = Path.GetFileName(archivePath);
        var rewrite = name.Equals(ProjectRootMarker, StringComparison.Ordinal)
            || archivePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || archivePath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)
            || archivePath.EndsWith(".log", StringComparison.OrdinalIgnoreCase);
        return new RestorePlacement(dest, rewrite);
    }

    private static string? LocateProjectDir(string tmpDir, string projectPath, SessionPlatform platform)
    {
        if (!Directory.Exists(tmpDir))
        {
            return null;
        }

        // Preferred: the newer slug scheme records the absolute path in a .project_root marker.
        foreach (var dir in Directory.EnumerateDirectories(tmpDir).OrderBy(d => d, StringComparer.Ordinal))
        {
            var marker = Path.Combine(dir, ProjectRootMarker);
            if (File.Exists(marker))
            {
                try
                {
                    var recorded = File.ReadAllText(marker).Trim();
                    if (SessionProviderSupport.PathsEqual(recorded, projectPath, platform))
                    {
                        return dir;
                    }
                }
                catch
                {
                    // Ignore unreadable markers.
                }
            }
        }

        // Legacy: directory named by the SHA-256 hex of the project path.
        var legacy = Path.Combine(tmpDir, LegacyHash(projectPath));
        return Directory.Exists(legacy) ? legacy : null;
    }

    internal static string LegacyHash(string projectPath)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(projectPath))).ToLowerInvariant();
}
