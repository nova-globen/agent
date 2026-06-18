using AgentSync.Core.Configuration;

namespace AgentSync.Core.Adapters;

/// <summary>
/// Renders a skill as a managed Markdown section appended into a shared instruction
/// file (AGENTS.md, CLAUDE.md, Copilot instructions, Gemini instructions).
/// </summary>
public sealed class SharedMarkdownAdapter : ISkillAdapter
{
    public SharedMarkdownAdapter(string targetId) => TargetId = targetId;

    public string TargetId { get; }

    public ProjectionMode Mode => ProjectionMode.SharedSection;

    public string ResolvePath(string configuredPath, Skill skill) => configuredPath;

    public string Render(Skill skill)
    {
        var m = skill.Manifest;
        var name = string.IsNullOrWhiteSpace(m.Name) ? skill.Id : m.Name!.Trim();
        var description = (m.Description ?? string.Empty).Trim();
        var body = skill.Body.Trim();

        var sb = new System.Text.StringBuilder();
        sb.Append("## ").Append(name).Append('\n');
        if (description.Length > 0)
        {
            sb.Append('\n').Append(description).Append('\n');
        }

        if (body.Length > 0)
        {
            sb.Append('\n').Append(body).Append('\n');
        }

        return sb.ToString().TrimEnd('\n');
    }
}
