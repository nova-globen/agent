using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSync.Core.Sessions;

/// <summary>One archived session file plus its integrity hash.</summary>
public sealed record SessionFileEntry(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("sha256")] string Sha256);

/// <summary>The environment a backup was captured in, recorded so restore can translate paths.</summary>
public sealed record SessionSource(
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("pathStyle")] string PathStyle,
    [property: JsonPropertyName("homeDirectory")] string HomeDirectory,
    [property: JsonPropertyName("projectPath")] string ProjectPath,
    [property: JsonPropertyName("storeKey")] string StoreKey);

/// <summary>
/// The manifest written at the root of every session backup archive. It records which agent
/// the sessions belong to, the environment they were captured in (so paths can be retargeted
/// on restore), and the integrity hashes of every archived file.
/// </summary>
public sealed record SessionManifest(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("agentSyncVersion")] string AgentSyncVersion,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("createdUtc")] string CreatedUtc,
    [property: JsonPropertyName("source")] SessionSource Source,
    [property: JsonPropertyName("files")] IReadOnlyList<SessionFileEntry> Files)
{
    public const string FileName = "manifest.json";
    public const int CurrentSchemaVersion = 1;

    /// <summary>Files inside the archive live under this directory.</summary>
    public const string FilesPrefix = "files/";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static SessionManifest Parse(string json)
    {
        var manifest = JsonSerializer.Deserialize<SessionManifest>(json, Options)
            ?? throw new SessionException("manifest.json is empty or invalid.");
        if (manifest.Source is null || manifest.Files is null || string.IsNullOrEmpty(manifest.Provider))
        {
            throw new SessionException("manifest.json is missing required fields.");
        }

        return manifest;
    }

    public static SessionPlatform ParsePlatform(string value) => value.ToLowerInvariant() switch
    {
        "windows" => SessionPlatform.Windows,
        "macos" => SessionPlatform.MacOs,
        _ => SessionPlatform.Linux,
    };

    public static string PlatformString(SessionPlatform platform) => platform switch
    {
        SessionPlatform.Windows => "windows",
        SessionPlatform.MacOs => "macos",
        _ => "linux",
    };
}

/// <summary>Raised for recoverable session backup/restore failures (bad archive, etc.).</summary>
public sealed class SessionException : Exception
{
    public SessionException(string message) : base(message)
    {
    }
}
