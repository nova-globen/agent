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
}
