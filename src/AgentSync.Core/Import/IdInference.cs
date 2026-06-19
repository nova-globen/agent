using System.Text;
using System.Text.RegularExpressions;

namespace AgentSync.Core.Import;

/// <summary>
/// Derives a safe canonical skill id (lowercase kebab-case) from free text such as a
/// display name, heading, or filename. Mirrors the id rule enforced by
/// <see cref="Configuration.SkillValidator"/>: <c>^[a-z0-9]+(?:-[a-z0-9]+)*$</c>.
/// </summary>
public static partial class IdInference
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex IdPattern();

    /// <summary>True if <paramref name="id"/> is already a valid canonical skill id.</summary>
    public static bool IsValid(string? id) => !string.IsNullOrEmpty(id) && IdPattern().IsMatch(id);

    /// <summary>
    /// Slugifies <paramref name="input"/> into a valid kebab-case id, or returns
    /// <c>null</c> when nothing usable can be derived (so callers reject rather than
    /// silently mangle).
    /// </summary>
    public static string? Slugify(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var sb = new StringBuilder(input.Length);
        var lastWasHyphen = false;
        foreach (var ch in input.Trim().ToLowerInvariant())
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(ch);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                // Collapse any run of separators/punctuation into a single hyphen.
                sb.Append('-');
                lastWasHyphen = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        return IsValid(slug) ? slug : null;
    }
}
