using System.Text;
using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;

namespace AgentSync.Core.Authoring;

/// <summary>
/// Renders and writes canonical skill files (<c>skill.yaml</c> + <c>SKILL.md</c>) under
/// <c>.agent/skills/&lt;id&gt;/</c>. Shared by import and the skill CRUD commands so both
/// produce byte-identical, deterministic output. YAML values go through
/// <see cref="Yaml.Scalar"/>; targets are emitted in canonical order.
/// </summary>
public static class SkillFiles
{
    /// <summary>Absolute path to the skill directory for <paramref name="id"/>.</summary>
    public static string SkillDir(RepoLayout layout, string id) => Path.Combine(layout.SkillsDir, id);

    /// <summary>True if a canonical skill already exists at <c>.agent/skills/&lt;id&gt;/</c>.</summary>
    public static bool Exists(RepoLayout layout, string id) => Directory.Exists(SkillDir(layout, id));

    /// <summary>Renders a deterministic <c>skill.yaml</c> document.</summary>
    public static string RenderManifestYaml(
        string id,
        string name,
        string description,
        string version,
        IEnumerable<string> enabledTargets)
    {
        var enabled = new HashSet<string>(enabledTargets, StringComparer.Ordinal);

        var sb = new StringBuilder();
        sb.Append("id: ").Append(Yaml.Scalar(id)).Append('\n');
        sb.Append("name: ").Append(Yaml.Scalar(name)).Append('\n');
        sb.Append("description: ").Append(Yaml.Scalar(description)).Append('\n');
        sb.Append("version: ").Append(Yaml.Scalar(version)).Append('\n');
        sb.Append('\n');
        sb.Append("targets:\n");
        foreach (var target in TargetIds.Ordered)
        {
            if (enabled.Contains(target))
            {
                sb.Append("  ").Append(target).Append(":\n");
                sb.Append("    enabled: true\n");
            }
        }

        return sb.ToString();
    }

    /// <summary>Writes <c>skill.yaml</c> and <c>SKILL.md</c> for a skill, creating the directory.</summary>
    public static void Write(RepoLayout layout, string id, string manifestYaml, string body)
    {
        var dir = SkillDir(layout, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "skill.yaml"), EnsureTrailingNewline(manifestYaml));
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), EnsureTrailingNewline(body));
    }

    private static string EnsureTrailingNewline(string content)
    {
        var normalized = content.Replace("\r\n", "\n");
        return normalized.EndsWith('\n') ? normalized : normalized + "\n";
    }
}
