using System.Text;

namespace AgentSync.Core.Subagents;

/// <summary>
/// Renders a canonical sub-agent to TOML format for the <c>toml_agent</c> projection target.
/// Path layout: <c>&lt;configured-path&gt;/&lt;id&gt;.toml</c>. Whole file generated.
/// </summary>
public static class TomlAgentRenderer
{
    public static string Render(Subagent agent)
    {
        var sb = new StringBuilder();
        sb.Append("name = ").Append(TomlLiteralString(agent.Id)).Append('\n');
        sb.Append("description = ").Append(TomlLiteralString(agent.Manifest.Description ?? string.Empty)).Append('\n');

        if (!string.IsNullOrWhiteSpace(agent.Manifest.Model))
        {
            sb.Append("model = ").Append(TomlLiteralString(agent.Manifest.Model!)).Append('\n');
        }

        var tools = agent.Manifest.Tools.Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        if (tools.Count > 0)
        {
            sb.Append("tools = [");
            sb.Append(string.Join(", ", tools.Select(TomlLiteralString)));
            sb.Append("]\n");
        }

        var body = agent.Body.TrimEnd('\n', '\r');
        sb.Append('\n');
        sb.Append(RenderSystemPrompt(body));
        return sb.ToString();
    }

    private static string RenderSystemPrompt(string body)
    {
        // Prefer TOML multi-line literal strings (single-quoted, no escaping) unless the body
        // contains the ''' terminator, in which case fall back to basic strings.
        if (!body.Contains("'''"))
        {
            return $"system_prompt = '''\n{body}\n'''\n";
        }

        // Multi-line basic string: escape embedded """ sequences and backslashes.
        var escaped = body
            .Replace("\\", "\\\\")
            .Replace("\"\"\"", "\"\"\\\"");
        return $"system_prompt = \"\"\"\n{escaped}\n\"\"\"\n";
    }

    /// <summary>
    /// Returns a TOML single-quoted literal string. Falls back to a basic (double-quoted)
    /// string for values containing single quotes or control characters.
    /// </summary>
    private static string TomlLiteralString(string value)
    {
        if (!value.Contains('\'') && !value.Any(c => c < 0x20 || c == 0x7f))
        {
            return $"'{value}'";
        }

        var escaped = value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
        return $"\"{escaped}\"";
    }
}
