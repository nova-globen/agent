using YamlDotNet.Serialization;

namespace AgentSync.Core.Configuration;

/// <summary>Per-target settings inside <c>agent.yaml</c>.</summary>
public sealed class TargetSetting
{
    public bool Enabled { get; set; }

    public string? Path { get; set; }
}

/// <summary>Repository-wide drift policy.</summary>
public sealed class AgentPolicy
{
    public bool FailOnMissingProjection { get; set; } = true;

    public bool FailOnOutdatedProjection { get; set; } = true;

    public bool FailOnManualEdit { get; set; } = true;

    public bool AllowTargetSpecificOverrides { get; set; } = true;
}

/// <summary>The parsed <c>.agent/agent.yaml</c> document.</summary>
public sealed class AgentConfig
{
    /// <summary>The latest schema version this build understands.</summary>
    public const int SupportedVersion = 1;

    public int Version { get; set; }

    public Dictionary<string, TargetSetting> Targets { get; set; } = new(StringComparer.Ordinal);

    public AgentPolicy Policy { get; set; } = new();

    /// <summary>
    /// Parses YAML text into an <see cref="AgentConfig"/>.
    /// Throws <see cref="ConfigParseException"/> if the YAML is malformed.
    /// </summary>
    public static AgentConfig Parse(string yaml)
    {
        try
        {
            var config = YamlSupport.Deserializer.Deserialize<AgentConfig>(yaml);
            return config ?? new AgentConfig();
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new ConfigParseException($"Invalid YAML: {ex.Message}", ex);
        }
    }
}
