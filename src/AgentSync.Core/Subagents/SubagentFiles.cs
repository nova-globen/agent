using System.Text;
using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;

namespace AgentSync.Core.Subagents;

/// <summary>
/// Renders and writes canonical sub-agent files (<c>agent.yaml</c> + <c>AGENT.md</c>) under
/// <c>.agent/agents/&lt;id&gt;/</c>, and renders the projected Claude Code sub-agent document
/// (<c>.claude/agents/&lt;id&gt;.md</c>). Shared by the CRUD writer, the importer, and the
/// projector so all three produce byte-identical, deterministic output (YAML via
/// <see cref="Yaml.Scalar"/>).
/// </summary>
public static class SubagentFiles
{
    public static string AgentDir(RepoLayout layout, string id) => Path.Combine(layout.AgentsDir, id);

    public static bool Exists(RepoLayout layout, string id) => Directory.Exists(AgentDir(layout, id));

    /// <summary>Renders a deterministic canonical <c>agent.yaml</c>.</summary>
    public static string RenderManifestYaml(
        string id,
        string name,
        string description,
        string? model,
        string? color,
        IEnumerable<string> tools)
    {
        var sb = new StringBuilder();
        sb.Append("id: ").Append(Yaml.Scalar(id)).Append('\n');
        sb.Append("name: ").Append(Yaml.Scalar(name)).Append('\n');
        sb.Append("description: ").Append(Yaml.Scalar(description)).Append('\n');
        if (!string.IsNullOrWhiteSpace(model))
        {
            sb.Append("model: ").Append(Yaml.Scalar(model)).Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(color))
        {
            sb.Append("color: ").Append(Yaml.Scalar(color)).Append('\n');
        }

        var toolList = tools.Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        if (toolList.Count > 0)
        {
            sb.Append("tools:\n");
            foreach (var tool in toolList)
            {
                sb.Append("  - ").Append(Yaml.Scalar(tool)).Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>Writes <c>agent.yaml</c> and <c>AGENT.md</c>, creating the directory.</summary>
    public static void Write(RepoLayout layout, string id, string manifestYaml, string body)
    {
        var dir = AgentDir(layout, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "agent.yaml"), EnsureTrailingNewline(manifestYaml));
        File.WriteAllText(Path.Combine(dir, "AGENT.md"), EnsureTrailingNewline(body));
    }

    /// <summary>
    /// Renders the projected Claude Code sub-agent document: YAML frontmatter (name = id,
    /// description, optional tools/model) followed by the system-prompt body.
    /// </summary>
    public static string RenderProjection(Subagent agent)
    {
        var sb = new StringBuilder();
        sb.Append("---\n");
        sb.Append("name: ").Append(Yaml.Scalar(agent.Id)).Append('\n');
        sb.Append("description: ").Append(Yaml.Scalar(agent.Manifest.Description ?? string.Empty)).Append('\n');

        var tools = agent.Manifest.Tools.Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        if (tools.Count > 0)
        {
            sb.Append("tools: ").Append(Yaml.Scalar(string.Join(", ", tools))).Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(agent.Manifest.Model))
        {
            sb.Append("model: ").Append(Yaml.Scalar(agent.Manifest.Model!)).Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(agent.Manifest.Color))
        {
            sb.Append("color: ").Append(Yaml.Scalar(agent.Manifest.Color!)).Append('\n');
        }

        sb.Append("---\n\n");
        sb.Append(SkillContent.StripRedundantHeading(agent.Body, agent.DisplayName).TrimEnd('\n'));
        sb.Append('\n');
        return sb.ToString();
    }

    /// <summary>Loads all canonical sub-agents under <c>.agent/agents/</c>, ordered by id.</summary>
    public static IReadOnlyList<Subagent> LoadAll(RepoLayout layout)
    {
        var result = new List<Subagent>();
        if (!Directory.Exists(layout.AgentsDir))
        {
            return result;
        }

        foreach (var dir in Directory.EnumerateDirectories(layout.AgentsDir).OrderBy(d => d, StringComparer.Ordinal))
        {
            var manifestPath = Path.Combine(dir, "agent.yaml");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            SubagentManifest manifest;
            try
            {
                manifest = SubagentManifest.Parse(File.ReadAllText(manifestPath));
            }
            catch (ConfigParseException)
            {
                continue;
            }

            var bodyPath = Path.Combine(dir, "AGENT.md");
            result.Add(new Subagent
            {
                Manifest = manifest,
                Body = File.Exists(bodyPath) ? File.ReadAllText(bodyPath) : string.Empty,
                DirectoryName = Path.GetFileName(dir),
                DirectoryPath = dir,
            });
        }

        return result;
    }

    private static string EnsureTrailingNewline(string content)
    {
        var normalized = content.Replace("\r\n", "\n");
        return normalized.EndsWith('\n') ? normalized : normalized + "\n";
    }
}
