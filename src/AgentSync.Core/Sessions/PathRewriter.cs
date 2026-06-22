using System.Text;

namespace AgentSync.Core.Sessions;

/// <summary>
/// Rewrites absolute paths embedded inside session file contents when a backup is restored
/// into a different environment or project location. Every spelling of the source location —
/// Unix, WSL, and Windows (back- and forward-slash) — is translated to the destination
/// location written in the destination's own native style, so a session captured on WSL
/// (<c>/mnt/c/Users/x/proj</c>) is rewritten to <c>C:\Users\x\new</c> when restored on
/// Windows, and vice-versa. In JSON content, Windows backslashes are emitted doubled so the
/// rewritten value stays valid JSON.
/// </summary>
public sealed class PathRewriter
{
    private readonly LocationPath? _srcProject;
    private readonly LocationPath? _dstProject;
    private readonly string _srcProjectRaw;
    private readonly string _dstProjectNative;
    private readonly LocationPath? _srcHome;
    private readonly LocationPath? _dstHome;
    private readonly string _srcHomeRaw;
    private readonly string _dstHomeNative;
    private readonly bool _identity;

    private PathRewriter(
        LocationPath? srcProject, LocationPath? dstProject, string srcProjectRaw, string dstProjectNative,
        LocationPath? srcHome, LocationPath? dstHome, string srcHomeRaw, string dstHomeNative,
        bool identity)
    {
        _srcProject = srcProject;
        _dstProject = dstProject;
        _srcProjectRaw = srcProjectRaw;
        _dstProjectNative = dstProjectNative;
        _srcHome = srcHome;
        _dstHome = dstHome;
        _srcHomeRaw = srcHomeRaw;
        _dstHomeNative = dstHomeNative;
        _identity = identity;
    }

    /// <summary>True when there is nothing to rewrite (source and destination coincide).</summary>
    public bool IsIdentity => _identity;

    public static PathRewriter Build(
        string sourceProject,
        string destProject,
        string? sourceHome = null,
        string? destHome = null)
    {
        var srcProject = LocationPath.Parse(sourceProject);
        var dstProject = LocationPath.Parse(destProject);
        var dstProjectNative = NativeRender(dstProject, destProject);

        var hasHome = !string.IsNullOrEmpty(sourceHome) && !string.IsNullOrEmpty(destHome);
        var srcHome = hasHome ? LocationPath.Parse(sourceHome!) : null;
        var dstHome = hasHome ? LocationPath.Parse(destHome!) : null;
        var dstHomeNative = hasHome ? NativeRender(dstHome, destHome!) : string.Empty;

        var projectSame = string.Equals(sourceProject, destProject, StringComparison.Ordinal);
        var homeSame = !hasHome || string.Equals(sourceHome, destHome, StringComparison.Ordinal);

        return new PathRewriter(
            srcProject, dstProject, sourceProject, dstProjectNative,
            srcHome, dstHome, sourceHome ?? string.Empty, dstHomeNative,
            projectSame && homeSame);
    }

    private static string NativeRender(LocationPath? loc, string raw)
    {
        if (loc is null)
        {
            return raw;
        }

        var native = PathConversion.DetectStyle(raw);
        return loc.Render(native) ?? loc.Render(PathStyle.Unix) ?? raw;
    }

    /// <summary>
    /// Applies the path translation to <paramref name="text"/>. When
    /// <paramref name="jsonEscaped"/> is true the content is treated as JSON: source Windows
    /// paths are matched in their escaped form and destination backslashes are doubled.
    /// </summary>
    public string Apply(string text, bool jsonEscaped)
    {
        if (_identity || string.IsNullOrEmpty(text))
        {
            return text;
        }

        var pairs = new List<(string From, string To)>();
        AddPairs(pairs, _srcProject, _srcProjectRaw, _dstProjectNative, jsonEscaped);
        AddPairs(pairs, _srcHome, _srcHomeRaw, _dstHomeNative, jsonEscaped);

        var ordered = pairs
            .Where(p => p.From.Length > 0 && !string.Equals(p.From, p.To, StringComparison.Ordinal))
            .GroupBy(p => p.From, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderByDescending(p => p.From.Length)
            .ToList();

        return ReplaceLeftToRight(text, ordered);
    }

    /// <summary>
    /// Single left-to-right scan applying the first (longest) matching replacement at each
    /// position and advancing past it, so a replacement's output is never itself rewritten
    /// (e.g. a Windows source path that is a prefix of the destination path).
    /// </summary>
    private static string ReplaceLeftToRight(string text, IReadOnlyList<(string From, string To)> pairs)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var matched = false;
            foreach (var (from, to) in pairs)
            {
                if (from.Length <= text.Length - i
                    && string.CompareOrdinal(text, i, from, 0, from.Length) == 0)
                {
                    sb.Append(to);
                    i += from.Length;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                sb.Append(text[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    private static void AddPairs(
        List<(string From, string To)> pairs,
        LocationPath? src,
        string srcRaw,
        string destNative,
        bool jsonEscaped)
    {
        var to = jsonEscaped ? destNative.Replace("\\", "\\\\") : destNative;

        if (src is null)
        {
            if (srcRaw.Length > 0)
            {
                pairs.Add((srcRaw, to));
            }

            return;
        }

        foreach (var style in new[] { PathStyle.Windows, PathStyle.WindowsForward, PathStyle.Wsl, PathStyle.Unix })
        {
            var from = src.Render(style);
            if (from is null)
            {
                continue;
            }

            if (jsonEscaped && style == PathStyle.Windows)
            {
                from = from.Replace("\\", "\\\\");
            }

            pairs.Add((from, to));
        }
    }
}
