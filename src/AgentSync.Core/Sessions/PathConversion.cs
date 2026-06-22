namespace AgentSync.Core.Sessions;

/// <summary>How an absolute path is written on a given platform.</summary>
public enum PathStyle
{
    /// <summary>POSIX path with no drive, e.g. <c>/home/user/proj</c>.</summary>
    Unix,

    /// <summary>WSL drive mount, e.g. <c>/mnt/c/Users/x/proj</c>.</summary>
    Wsl,

    /// <summary>Windows backslash drive path, e.g. <c>C:\Users\x\proj</c>.</summary>
    Windows,

    /// <summary>Windows drive path written with forward slashes, e.g. <c>C:/Users/x/proj</c>.</summary>
    WindowsForward,
}

/// <summary>
/// A platform-neutral decomposition of an absolute path into an optional drive letter and
/// a list of path segments, plus the rendering of that location in every known
/// <see cref="PathStyle"/>. This is the heart of cross-environment session restore: a path
/// captured on WSL (<c>/mnt/c/...</c>) can be re-rendered as a Windows path (<c>C:\...</c>)
/// and vice-versa, and embedded path strings inside session files can be translated in
/// whichever style they were originally written.
/// </summary>
public sealed record LocationPath(char? Drive, IReadOnlyList<string> Segments)
{
    public bool HasDrive => Drive is not null;

    /// <summary>
    /// Parses an absolute path in Unix, WSL, or Windows form. Returns <c>null</c> for
    /// relative paths or anything that is not recognisably absolute.
    /// </summary>
    public static LocationPath? Parse(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var p = path.Trim();

        // Windows drive path: C:\... or C:/... (also bare "C:").
        if (p.Length >= 2 && char.IsLetter(p[0]) && p[1] == ':')
        {
            var drive = char.ToUpperInvariant(p[0]);
            var rest = p.Length > 2 ? p[2..] : string.Empty;
            return new LocationPath(drive, SplitSegments(rest));
        }

        // WSL drive mount: /mnt/<letter>/...  (also bare "/mnt/c").
        if ((p.StartsWith("/mnt/", StringComparison.Ordinal) && p.Length >= 6 && char.IsLetter(p[5])
                && (p.Length == 6 || p[6] == '/'))
            || (p.Length == 6 && p.StartsWith("/mnt/", StringComparison.Ordinal) && char.IsLetter(p[5])))
        {
            var drive = char.ToUpperInvariant(p[5]);
            var rest = p.Length > 6 ? p[6..] : string.Empty;
            return new LocationPath(drive, SplitSegments(rest));
        }

        // POSIX absolute path.
        if (p.StartsWith('/'))
        {
            return new LocationPath(null, SplitSegments(p));
        }

        return null;
    }

    private static List<string> SplitSegments(string rest)
        => rest.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>
    /// Renders this location in the requested style, or <c>null</c> when the style cannot
    /// represent it (for example a drive-less POSIX path has no native Windows form, and a
    /// drive-bearing path has no pure-Unix form).
    /// </summary>
    public string? Render(PathStyle style)
    {
        switch (style)
        {
            case PathStyle.Unix:
                return HasDrive ? null : "/" + string.Join('/', Segments);

            case PathStyle.Wsl:
                return HasDrive
                    ? "/mnt/" + char.ToLowerInvariant(Drive!.Value) + JoinLeading('/')
                    : "/" + string.Join('/', Segments);

            case PathStyle.Windows:
                return HasDrive
                    ? Drive!.Value + ":" + (Segments.Count == 0 ? "\\" : "\\" + string.Join('\\', Segments))
                    : null;

            case PathStyle.WindowsForward:
                return HasDrive
                    ? Drive!.Value + ":" + (Segments.Count == 0 ? "/" : "/" + string.Join('/', Segments))
                    : null;

            default:
                return null;
        }
    }

    private string JoinLeading(char sep)
        => Segments.Count == 0 ? string.Empty : sep + string.Join(sep, Segments);
}

/// <summary>Helpers for detecting a path's native style and translating between styles.</summary>
public static class PathConversion
{
    /// <summary>
    /// Detects the style a path string is written in. Defaults to <see cref="PathStyle.Unix"/>
    /// for unrecognised input so callers always get a usable rendering target.
    /// </summary>
    public static PathStyle DetectStyle(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PathStyle.Unix;
        }

        var p = path.Trim();
        if (p.Length >= 2 && char.IsLetter(p[0]) && p[1] == ':')
        {
            return p.Contains('\\') ? PathStyle.Windows : PathStyle.WindowsForward;
        }

        if (p.StartsWith("/mnt/", StringComparison.Ordinal) && p.Length >= 6 && char.IsLetter(p[5])
            && (p.Length == 6 || p[6] == '/'))
        {
            return PathStyle.Wsl;
        }

        return PathStyle.Unix;
    }
}
