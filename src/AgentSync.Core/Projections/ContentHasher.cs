using System.Security.Cryptography;
using System.Text;

namespace AgentSync.Core.Projections;

/// <summary>
/// Produces stable, normalized content hashes used in section markers and the lockfile.
/// Normalization makes the hash insensitive to line-ending style and trailing
/// whitespace so cosmetic differences are not reported as drift.
/// </summary>
public static class ContentHasher
{
    public const string Prefix = "sha256:";

    /// <summary>
    /// Normalizes text: LF line endings, trailing whitespace removed from each line,
    /// and surrounding blank lines trimmed.
    /// </summary>
    public static string Normalize(string content)
    {
        var unified = content.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = unified.Split('\n').Select(line => line.TrimEnd());
        return string.Join('\n', lines).Trim('\n');
    }

    /// <summary>Returns <c>sha256:&lt;hex&gt;</c> for the normalized content.</summary>
    public static string Hash(string content)
    {
        var normalized = Normalize(content);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Prefix + Convert.ToHexStringLower(bytes);
    }

    /// <summary>True if the content's normalized hash equals <paramref name="expectedHash"/>.</summary>
    public static bool Matches(string content, string expectedHash)
        => string.Equals(Hash(content), expectedHash, StringComparison.Ordinal);

    /// <summary>
    /// Hashes <paramref name="body"/> together with all files in <paramref name="assetSourceDir"/>
    /// (sorted by relative path). When <paramref name="assetSourceDir"/> is null or the directory
    /// does not exist, falls back to <see cref="Hash(string)"/>. This produces a stable combined
    /// hash so that a change in any asset file shows up as drift.
    /// </summary>
    public static string HashWithAssets(string body, string? assetSourceDir)
    {
        if (assetSourceDir is null || !Directory.Exists(assetSourceDir))
        {
            return Hash(body);
        }

        var sb = new StringBuilder(Normalize(body));
        var files = Directory.GetFiles(assetSourceDir, "*", SearchOption.AllDirectories)
            .Select(f => (rel: Path.GetRelativePath(assetSourceDir, f).Replace('\\', '/'), abs: f))
            .OrderBy(x => x.rel, StringComparer.Ordinal);
        foreach (var (rel, abs) in files)
        {
            sb.Append('\0').Append(rel).Append('\0').Append(Normalize(File.ReadAllText(abs)));
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Prefix + Convert.ToHexStringLower(bytes);
    }
}
