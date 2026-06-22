using System.Text.Json;

namespace AgentSync.Core.Sessions.Providers;

/// <summary>
/// OpenAI Codex CLI stores rollouts under <c>~/.codex/sessions/YYYY/MM/DD/rollout-*.jsonl</c>,
/// organised by date rather than by project. Each rollout's first JSONL line is a
/// <c>session_meta</c> payload that records the <c>cwd</c> the session ran in, so the
/// project's sessions are found by matching that <c>cwd</c>. Restore preserves the date tree
/// and rewrites the embedded <c>cwd</c> for the destination.
/// </summary>
public sealed class CodexSessionProvider : ISessionProvider
{
    public string Id => "codex";
    public string DisplayName => "OpenAI Codex";
    public IReadOnlyList<string> Aliases => new[] { "openai-codex" };
    public bool Experimental => false;

    public string Root(SessionEnvironment env)
    {
        var overrideHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        return string.IsNullOrEmpty(overrideHome)
            ? Path.Combine(env.HomeDirectory, ".codex")
            : overrideHome;
    }

    public SessionCollection Collect(SessionEnvironment env, string projectPath)
    {
        var sessionsDir = Path.Combine(Root(env), "sessions");
        var entries = new List<SessionEntry>();
        if (!Directory.Exists(sessionsDir))
        {
            return SessionCollection.Empty("sessions");
        }

        foreach (var file in Directory.EnumerateFiles(sessionsDir, "rollout-*.jsonl", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            var cwd = ReadRolloutCwd(file);
            if (cwd is not null && SessionProviderSupport.PathsEqual(cwd, projectPath, env.Platform))
            {
                var rel = Path.GetRelativePath(sessionsDir, file).Replace('\\', '/');
                entries.Add(new SessionEntry($"sessions/{rel}", file));
            }
        }

        return new SessionCollection("sessions", entries);
    }

    public RestorePlacement? Place(
        SessionManifest manifest,
        SessionEnvironment destEnv,
        string destProjectPath,
        string archivePath)
    {
        var relative = archivePath.Replace('/', Path.DirectorySeparatorChar);
        var dest = Path.Combine(Root(destEnv), relative);
        return new RestorePlacement(dest, RewriteText: true);
    }

    /// <summary>Reads the <c>cwd</c> from a rollout's session-meta line (scans the first few lines).</summary>
    internal static string? ReadRolloutCwd(string file)
    {
        try
        {
            using var reader = new StreamReader(file);
            for (var i = 0; i < 8; i++)
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

                var cwd = ExtractCwd(line);
                if (cwd is not null)
                {
                    return cwd;
                }
            }
        }
        catch
        {
            // Unreadable/locked file: treat as no match rather than failing the whole backup.
        }

        return null;
    }

    private static string? ExtractCwd(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (TryGetString(root, "cwd", out var cwd))
            {
                return cwd;
            }

            if (root.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object
                && TryGetString(payload, "cwd", out var pcwd))
            {
                return pcwd;
            }
        }
        catch (JsonException)
        {
            // Not a JSON object line.
        }

        return null;
    }

    private static bool TryGetString(JsonElement obj, string name, out string? value)
    {
        value = null;
        if (obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
        {
            value = el.GetString();
            return !string.IsNullOrEmpty(value);
        }

        return false;
    }
}
