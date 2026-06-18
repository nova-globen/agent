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
}
