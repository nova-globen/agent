using System.Text.RegularExpressions;

namespace AgentSync.Core.Import;

/// <summary>A Markdown section: an optional heading (null for content before the first heading) and its body.</summary>
public sealed record MarkdownSection(string? Title, int Level, string Body);

/// <summary>
/// Splits a Markdown document into sections at its top-level headings (the shallowest
/// heading level present). Content before the first heading is returned as a section
/// with a null title.
/// </summary>
public static partial class HeadingSplitter
{
    [GeneratedRegex(@"^(?<hashes>#{1,6})\s+(?<title>.+?)\s*#*\s*$", RegexOptions.Multiline)]
    private static partial Regex HeadingLine();

    public static IReadOnlyList<MarkdownSection> Split(string markdown)
    {
        var normalized = markdown.Replace("\r\n", "\n");
        var matches = HeadingLine().Matches(normalized);

        if (matches.Count == 0)
        {
            var only = normalized.Trim();
            return only.Length == 0
                ? Array.Empty<MarkdownSection>()
                : new[] { new MarkdownSection(null, 0, only) };
        }

        var topLevel = matches.Min(m => m.Groups["hashes"].Value.Length);
        var splits = matches.Where(m => m.Groups["hashes"].Value.Length == topLevel).ToList();

        var sections = new List<MarkdownSection>();

        var firstStart = splits[0].Index;
        var preamble = normalized[..firstStart].Trim();
        if (preamble.Length > 0)
        {
            sections.Add(new MarkdownSection(null, 0, preamble));
        }

        for (var i = 0; i < splits.Count; i++)
        {
            var heading = splits[i];
            var title = heading.Groups["title"].Value.Trim();
            var bodyStart = heading.Index + heading.Length;
            var bodyEnd = i + 1 < splits.Count ? splits[i + 1].Index : normalized.Length;
            var body = normalized[bodyStart..bodyEnd].Trim();
            sections.Add(new MarkdownSection(title, topLevel, body));
        }

        return sections;
    }
}
