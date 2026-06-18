namespace AgentSync.Core;

/// <summary>Thrown when a repository-relative path is unsafe (absolute or escaping the root).</summary>
public sealed class RepoPathException : Exception
{
    public RepoPathException(string message) : base(message)
    {
    }
}

/// <summary>
/// Central, defensive resolution of repository-relative paths. Every read or write of a
/// projection target must go through here so that absolute paths, Windows drive/UNC
/// paths, and <c>..</c> traversal that would escape the repository root are rejected.
/// </summary>
public static class RepoPath
{
    /// <summary>
    /// Validates the <em>shape</em> of a relative path without needing a root: it must be
    /// non-empty, not absolute, not a Windows drive (<c>C:\</c>) or UNC (<c>\\server</c>)
    /// path, and must not escape upward via <c>..</c>.
    /// </summary>
    public static bool IsSafeRelative(string? relativePath, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            error = "path is empty.";
            return false;
        }

        // Treat both separators uniformly so Windows-style inputs are caught on every OS.
        var normalized = relativePath.Replace('\\', '/');

        if (normalized.StartsWith('/'))
        {
            error = $"path '{relativePath}' is absolute.";
            return false;
        }

        if (normalized.StartsWith("//", StringComparison.Ordinal))
        {
            error = $"path '{relativePath}' is a UNC path.";
            return false;
        }

        if (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':')
        {
            error = $"path '{relativePath}' is an absolute Windows path.";
            return false;
        }

        var depth = 0;
        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                depth--;
                if (depth < 0)
                {
                    error = $"path '{relativePath}' escapes the repository root.";
                    return false;
                }
            }
            else
            {
                depth++;
            }
        }

        return true;
    }

    /// <summary>True if <paramref name="relativePath"/> is a safe repository-relative path.</summary>
    public static bool IsSafeRelative(string? relativePath) => IsSafeRelative(relativePath, out _);

    /// <summary>
    /// Resolves <paramref name="relativePath"/> to an absolute path under
    /// <paramref name="repoRoot"/>, throwing <see cref="RepoPathException"/> if the path is
    /// unsafe or the resolved path lands outside the root.
    /// </summary>
    public static string Resolve(string repoRoot, string relativePath)
    {
        if (!IsSafeRelative(relativePath, out var error))
        {
            throw new RepoPathException(error!);
        }

        var root = Path.GetFullPath(repoRoot);
        var combined = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', '/')));

        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!string.Equals(combined, root, comparison)
            && !combined.StartsWith(rootWithSep, comparison))
        {
            throw new RepoPathException($"path '{relativePath}' escapes the repository root.");
        }

        return combined;
    }
}
