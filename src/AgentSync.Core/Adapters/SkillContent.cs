namespace AgentSync.Core.Adapters;

/// <summary>Helpers for shaping the canonical SKILL.md body before projection.</summary>
public static class SkillContent
{
    /// <summary>
    /// Returns the body with a leading top-level heading removed when it merely repeats
    /// the skill's display name. Display metadata (name) lives in <c>skill.yaml</c>; each
    /// adapter adds at most one target-appropriate heading, so a duplicate <c># Name</c>
    /// at the top of <c>SKILL.md</c> is stripped to avoid two identical headings.
    /// </summary>
    public static string StripRedundantHeading(string body, string name)
    {
        var trimmed = body.Replace("\r\n", "\n").TrimStart('\n');
        var newline = trimmed.IndexOf('\n');
        var firstLine = newline < 0 ? trimmed : trimmed[..newline];

        if (!firstLine.StartsWith("# ", StringComparison.Ordinal))
        {
            return body.Trim();
        }

        var headingText = firstLine[2..].Trim();
        if (!string.Equals(headingText, name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return body.Trim();
        }

        var rest = newline < 0 ? string.Empty : trimmed[(newline + 1)..];
        return rest.TrimStart('\n').TrimEnd();
    }
}
