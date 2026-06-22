using AgentSync.Core.Configuration;
using YamlDotNet.Serialization;

namespace AgentSync.Core.Subagents;

/// <summary>
/// The parsed <c>agent.yaml</c> for a canonical sub-agent: the metadata an agent tool needs
/// to register a delegate ("sub-agent"), without the Markdown system-prompt body.
/// </summary>
public sealed class SubagentManifest
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Optional model hint (e.g. <c>sonnet</c>, <c>opus</c>, <c>haiku</c>, <c>inherit</c>).</summary>
    public string? Model { get; set; }

    /// <summary>Optional allow-list of tools the sub-agent may use; empty means "inherit all".</summary>
    public List<string> Tools { get; set; } = new();

    public static SubagentManifest Parse(string yaml)
    {
        try
        {
            return YamlSupport.Deserializer.Deserialize<SubagentManifest>(yaml) ?? new SubagentManifest();
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new ConfigParseException($"Invalid YAML: {ex.Message}", ex);
        }
    }
}

/// <summary>A fully loaded canonical sub-agent: manifest plus Markdown body and source paths.</summary>
public sealed class Subagent
{
    public required SubagentManifest Manifest { get; init; }

    /// <summary>The system-prompt body from <c>AGENT.md</c>.</summary>
    public required string Body { get; init; }

    /// <summary>Directory name under <c>.agent/agents/</c>.</summary>
    public required string DirectoryName { get; init; }

    /// <summary>Absolute path to the sub-agent directory.</summary>
    public required string DirectoryPath { get; init; }

    public string Id => Manifest.Id ?? DirectoryName;

    public string DisplayName => string.IsNullOrWhiteSpace(Manifest.Name) ? Id : Manifest.Name!;
}
