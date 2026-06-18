using YamlDotNet.Serialization;

namespace AgentSync.Core.Configuration;

/// <summary>Per-target toggle inside a skill's <c>skill.yaml</c>.</summary>
public sealed class SkillTargetSetting
{
    public bool Enabled { get; set; }
}

/// <summary>The parsed <c>skill.yaml</c> metadata (without the Markdown body).</summary>
public sealed class SkillManifest
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Version { get; set; }

    public Dictionary<string, SkillTargetSetting> Targets { get; set; } = new(StringComparer.Ordinal);

    public static SkillManifest Parse(string yaml)
    {
        try
        {
            var manifest = YamlSupport.Deserializer.Deserialize<SkillManifest>(yaml);
            return manifest ?? new SkillManifest();
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new ConfigParseException($"Invalid YAML: {ex.Message}", ex);
        }
    }
}

/// <summary>A fully loaded canonical skill: manifest plus Markdown body and source paths.</summary>
public sealed class Skill
{
    public required SkillManifest Manifest { get; init; }

    /// <summary>The contents of <c>SKILL.md</c>.</summary>
    public required string Body { get; init; }

    /// <summary>Directory name under <c>.agent/skills/</c> (used for id/folder checks).</summary>
    public required string DirectoryName { get; init; }

    /// <summary>Absolute path to the skill directory.</summary>
    public required string DirectoryPath { get; init; }

    public string Id => Manifest.Id ?? DirectoryName;

    /// <summary>Returns the enabled target ids for this skill.</summary>
    public IEnumerable<string> EnabledTargets =>
        Manifest.Targets.Where(kv => kv.Value.Enabled).Select(kv => kv.Key);
}
