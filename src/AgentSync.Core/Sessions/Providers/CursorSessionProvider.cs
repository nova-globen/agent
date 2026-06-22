using System.Text.Json;

namespace AgentSync.Core.Sessions.Providers;

/// <summary>
/// Cursor stores chat/composer history in a per-workspace SQLite database under the editor's
/// <c>User/workspaceStorage/&lt;hash&gt;/</c> directory, where each workspace folder is recorded
/// in a sidecar <c>workspace.json</c>. Sessions for a project are found by matching that
/// recorded folder; the whole workspace-storage folder (including <c>state.vscdb</c>) is
/// archived. Experimental: the storage is a binary database keyed by a hash of the workspace
/// URI, so paths embedded inside it are not rewritten and a moved project is restored under
/// its original key.
/// </summary>
public sealed class CursorSessionProvider : ISessionProvider
{
    public string Id => "cursor";
    public string DisplayName => "Cursor";
    public IReadOnlyList<string> Aliases => Array.Empty<string>();
    public bool Experimental => true;

    public string Root(SessionEnvironment env)
    {
        var user = env.Platform switch
        {
            SessionPlatform.Windows => Path.Combine(env.HomeDirectory, "AppData", "Roaming", "Cursor", "User"),
            SessionPlatform.MacOs => Path.Combine(env.HomeDirectory, "Library", "Application Support", "Cursor", "User"),
            _ => Path.Combine(env.HomeDirectory, ".config", "Cursor", "User"),
        };
        return user;
    }

    public SessionCollection Collect(SessionEnvironment env, string projectPath)
    {
        var storage = Path.Combine(Root(env), "workspaceStorage");
        var entries = new List<SessionEntry>();
        if (!Directory.Exists(storage))
        {
            return SessionCollection.Empty("workspaceStorage");
        }

        string? matchedHash = null;
        foreach (var dir in Directory.EnumerateDirectories(storage).OrderBy(d => d, StringComparer.Ordinal))
        {
            var workspaceJson = Path.Combine(dir, "workspace.json");
            if (!File.Exists(workspaceJson))
            {
                continue;
            }

            var folder = ReadFolder(workspaceJson);
            if (folder is not null && SessionProviderSupport.PathsEqual(folder, projectPath, env.Platform))
            {
                matchedHash = Path.GetFileName(dir);
                foreach (var entry in SessionProviderSupport.EnumerateRelative(dir, $"workspaceStorage/{matchedHash}"))
                {
                    entries.Add(entry);
                }

                break;
            }
        }

        return new SessionCollection(matchedHash ?? "workspaceStorage", entries);
    }

    public RestorePlacement? Place(
        SessionManifest manifest,
        SessionEnvironment destEnv,
        string destProjectPath,
        string archivePath)
    {
        // The SQLite store is binary and keyed by a workspace-URI hash we cannot recompute
        // reliably, so files are restored under their original hash without content rewriting.
        var dest = Path.Combine(Root(destEnv), archivePath.Replace('/', Path.DirectorySeparatorChar));
        return new RestorePlacement(dest, RewriteText: false);
    }

    /// <summary>Reads and decodes the workspace folder path from a Cursor <c>workspace.json</c>.</summary>
    internal static string? ReadFolder(string workspaceJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(workspaceJson));
            if (!doc.RootElement.TryGetProperty("folder", out var folder) || folder.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return DecodeFileUri(folder.GetString()!);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Turns a <c>file://</c> workspace URI into an OS path for comparison.</summary>
    internal static string DecodeFileUri(string uri)
    {
        var s = uri;
        if (s.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            s = s["file://".Length..];
        }

        s = Uri.UnescapeDataString(s);

        // file:///c:/...  ->  c:/...  (drop the leading slash before a Windows drive).
        if (s.Length >= 3 && s[0] == '/' && char.IsLetter(s[1]) && s[2] == ':')
        {
            s = s[1..];
        }

        return s;
    }
}
