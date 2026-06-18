using AgentSync.Core.Configuration;

namespace AgentSync.Core.Adapters;

/// <summary>
/// Renders a skill as a skill-folder <c>SKILL.md</c> with YAML frontmatter
/// (<c>name</c> / <c>description</c>), used by both the OpenAI/ChatGPT and Claude
/// skill folders. Path layout: <c>&lt;base&gt;/&lt;id&gt;/SKILL.md</c>. Whole file generated.
/// </summary>
public sealed class SkillFolderAdapter : ISkillAdapter
{
    public SkillFolderAdapter(string targetId) => TargetId = targetId;

    public string TargetId { get; }

    public ProjectionMode Mode => ProjectionMode.WholeFile;

    public string ResolvePath(string configuredPath, Skill skill)
        => CursorAdapter.CombineRelative(configuredPath, $"{skill.Id}/SKILL.md");

    public string Render(Skill skill)
    {
        var m = skill.Manifest;
        var name = string.IsNullOrWhiteSpace(m.Name) ? skill.Id : m.Name!.Trim();
        var description = (m.Description ?? string.Empty).Trim();
        var body = SkillContent.StripRedundantHeading(skill.Body, name);

        var sb = new System.Text.StringBuilder();
        sb.Append("---\n");
        sb.Append("name: ").Append(Yaml.Scalar(name)).Append('\n');
        sb.Append("description: ").Append(Yaml.Scalar(description)).Append('\n');
        sb.Append("---\n");
        if (body.Length > 0)
        {
            sb.Append('\n').Append(body).Append('\n');
        }

        return sb.ToString().TrimEnd('\n') + "\n";
    }
}
