using AgentSync.Core.Configuration;

namespace AgentSync.Core.Adapters;

/// <summary>
/// Renders a skill as a Cursor rule file <c>.cursor/rules/&lt;id&gt;.mdc</c> with the
/// frontmatter Cursor expects. The whole file is generated.
/// </summary>
public sealed class CursorAdapter : ISkillAdapter
{
    public string TargetId => TargetIds.Cursor;

    public ProjectionMode Mode => ProjectionMode.WholeFile;

    public string ResolvePath(string configuredPath, Skill skill)
        => CombineRelative(configuredPath, $"{skill.Id}.mdc");

    public string Render(Skill skill)
    {
        var m = skill.Manifest;
        var name = string.IsNullOrWhiteSpace(m.Name) ? skill.Id : m.Name!.Trim();
        var description = (m.Description ?? string.Empty).Trim();
        var body = SkillContent.StripRedundantHeading(skill.Body, name);

        var sb = new System.Text.StringBuilder();
        sb.Append("---\n");
        sb.Append("description: ").Append(Yaml.Scalar(description)).Append('\n');
        sb.Append("globs:\n");
        sb.Append("alwaysApply: false\n");
        sb.Append("---\n\n");
        sb.Append("# ").Append(name).Append('\n');
        if (body.Length > 0)
        {
            sb.Append('\n').Append(body).Append('\n');
        }

        return sb.ToString().TrimEnd('\n') + "\n";
    }

    internal static string CombineRelative(string baseDir, string fileName)
    {
        var normalized = baseDir.Replace('\\', '/').TrimEnd('/');
        return normalized.Length == 0 ? fileName : $"{normalized}/{fileName}";
    }
}
