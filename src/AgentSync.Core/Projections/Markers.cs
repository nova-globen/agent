using System.Text.RegularExpressions;

namespace AgentSync.Core.Projections;

/// <summary>
/// Generated-section markers, e.g.:
/// <code>
/// &lt;!-- agent-sync:start id=code-review target=agents_md hash=sha256:abc... --&gt;
/// ...generated content...
/// &lt;!-- agent-sync:end --&gt;
/// </code>
/// </summary>
public static partial class Markers
{
    [GeneratedRegex(
        @"<!--\s*agent-sync:start\s+id=(?<id>[^\s]+)\s+target=(?<target>[^\s]+)\s+hash=(?<hash>sha256:[0-9a-f]+)\s*-->",
        RegexOptions.Compiled)]
    public static partial Regex StartMarker();

    [GeneratedRegex(@"<!--\s*agent-sync:end\s*-->", RegexOptions.Compiled)]
    public static partial Regex EndMarker();

    public static string RenderStart(string skillId, string targetId, string hash)
        => $"<!-- agent-sync:start id={skillId} target={targetId} hash={hash} -->";

    public const string End = "<!-- agent-sync:end -->";

    // A section body may legitimately contain literal agent-sync marker comments (for
    // example a skill that documents the marker format). Left as-is, an inner
    // "<!-- agent-sync:end -->" would be parsed as the section's own end marker and
    // truncate it, so every projection of that skill would look manually edited. We escape
    // the opening "<!--" of any inner marker to "&lt;!--" when writing into a shared file
    // and reverse it when reading, so the body round-trips exactly (and hashes stay stable)
    // while never colliding with the section markers. "&lt;" renders as "<", so the visible
    // output is unchanged.
    [GeneratedRegex(@"<!--(?=\s*agent-sync:(?:start|end)\b)", RegexOptions.Compiled)]
    private static partial Regex BodyMarkerOpen();

    [GeneratedRegex(@"&lt;!--(?=\s*agent-sync:(?:start|end)\b)", RegexOptions.Compiled)]
    private static partial Regex BodyMarkerEscapedOpen();

    /// <summary>Escapes inner agent-sync marker comments in a section body so they cannot
    /// be mistaken for the section's own markers. Reversed by <see cref="UnescapeBody"/>.</summary>
    public static string EscapeBody(string body) => BodyMarkerOpen().Replace(body, "&lt;!--");

    /// <summary>Reverses <see cref="EscapeBody"/>.</summary>
    public static string UnescapeBody(string body) => BodyMarkerEscapedOpen().Replace(body, "<!--");
}
