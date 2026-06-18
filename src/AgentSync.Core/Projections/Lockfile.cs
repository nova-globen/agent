using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSync.Core.Projections;

/// <summary>A recorded projection: which skill was written to which target file, and its hash.</summary>
public sealed class LockEntry
{
    public string Skill { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;
}

/// <summary>
/// The <c>.agent/lock.json</c> model: the last-known-good hashes for every projection,
/// keyed by <c>"&lt;skill&gt;/&lt;target&gt;"</c>.
/// </summary>
public sealed class Lockfile
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public Dictionary<string, LockEntry> Projections { get; set; } = new(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string KeyFor(string skill, string target) => $"{skill}/{target}";

    public void Record(string skill, string target, string path, string hash)
    {
        Projections[KeyFor(skill, target)] = new LockEntry
        {
            Skill = skill,
            Target = target,
            Path = path.Replace('\\', '/'),
            Hash = hash,
        };
    }

    public LockEntry? Get(string skill, string target)
        => Projections.TryGetValue(KeyFor(skill, target), out var entry) ? entry : null;

    public string Serialize() => JsonSerializer.Serialize(this, Options);

    public static Lockfile Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Lockfile();
        }

        return JsonSerializer.Deserialize<Lockfile>(json, Options) ?? new Lockfile();
    }

    public static Lockfile Load(string path)
        => File.Exists(path) ? Parse(File.ReadAllText(path)) : new Lockfile();

    public void Save(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        File.WriteAllText(path, Serialize() + "\n");
    }
}
