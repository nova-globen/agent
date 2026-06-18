using System.Text;
using System.Text.RegularExpressions;

namespace AgentSync.Core.Adapters;

/// <summary>
/// Emits YAML scalar values for generated frontmatter. Values that are not safe as a
/// plain (unquoted) scalar are double-quoted with proper escaping, so user-supplied
/// names and descriptions containing <c>:</c>, <c>#</c>, quotes, or newlines always
/// produce valid YAML rather than being hand-concatenated.
/// </summary>
public static partial class Yaml
{
    // A conservative set of values safe to emit unquoted: must start with a letter or
    // digit and contain only characters that carry no special meaning in a YAML plain
    // scalar. Anything else (':', '#', quotes, newlines, leading/trailing spaces, ...)
    // forces double-quoting.
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 _.,()/+-]*$")]
    private static partial Regex SafePlainScalar();

    /// <summary>Returns a YAML-safe scalar representation of <paramref name="value"/>.</summary>
    public static string Scalar(string? value)
    {
        value ??= string.Empty;

        if (value.Length > 0
            && !value.EndsWith(' ')
            && SafePlainScalar().IsMatch(value))
        {
            return value;
        }

        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }
}
