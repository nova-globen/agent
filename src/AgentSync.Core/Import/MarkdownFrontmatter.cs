namespace AgentSync.Core.Import;

/// <summary>The result of splitting a Markdown document into optional YAML frontmatter and body.</summary>
public sealed record FrontmatterSplit(string? Frontmatter, string Body)
{
    public bool HasFrontmatter => Frontmatter is not null;
}

/// <summary>
/// Splits leading <c>---</c>-delimited YAML frontmatter from a Markdown body. This reads
/// back the frontmatter that <see cref="Adapters.SkillFolderAdapter"/> and
/// <see cref="Adapters.CursorAdapter"/> write.
/// </summary>
public static class MarkdownFrontmatter
{
    public static FrontmatterSplit Split(string content)
    {
        var normalized = content.Replace("\r\n", "\n");

        if (!normalized.StartsWith("---\n", StringComparison.Ordinal)
            && !normalized.Equals("---", StringComparison.Ordinal))
        {
            return new FrontmatterSplit(null, content);
        }

        // Find the closing '---' line after the opening one.
        var lines = normalized.Split('\n');
        // lines[0] == "---". Search for the next line equal to "---".
        var close = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i] == "---")
            {
                close = i;
                break;
            }
        }

        if (close < 0)
        {
            // No closing delimiter: treat the whole thing as body (not valid frontmatter).
            return new FrontmatterSplit(null, content);
        }

        var frontmatter = string.Join('\n', lines[1..close]);
        var body = string.Join('\n', lines[(close + 1)..]);
        return new FrontmatterSplit(frontmatter, body.TrimStart('\n'));
    }
}
