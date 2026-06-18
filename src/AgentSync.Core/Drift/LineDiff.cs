using System.Text;

namespace AgentSync.Core.Drift;

/// <summary>A minimal LCS-based line diff producing unified-style output.</summary>
public static class LineDiff
{
    /// <summary>
    /// Returns a line diff between <paramref name="oldText"/> and <paramref name="newText"/>.
    /// Lines are prefixed with "  " (unchanged), "- " (removed), or "+ " (added).
    /// Returns an empty string if the normalized texts are identical.
    /// </summary>
    public static string Compute(string oldText, string newText)
    {
        var oldLines = Split(oldText);
        var newLines = Split(newText);

        if (oldLines.SequenceEqual(newLines))
        {
            return string.Empty;
        }

        var lcs = LongestCommonSubsequence(oldLines, newLines);

        var sb = new StringBuilder();
        int i = 0, j = 0;
        foreach (var common in lcs)
        {
            while (i < oldLines.Count && oldLines[i] != common)
            {
                sb.Append("- ").Append(oldLines[i]).Append('\n');
                i++;
            }

            while (j < newLines.Count && newLines[j] != common)
            {
                sb.Append("+ ").Append(newLines[j]).Append('\n');
                j++;
            }

            sb.Append("  ").Append(common).Append('\n');
            i++;
            j++;
        }

        while (i < oldLines.Count)
        {
            sb.Append("- ").Append(oldLines[i]).Append('\n');
            i++;
        }

        while (j < newLines.Count)
        {
            sb.Append("+ ").Append(newLines[j]).Append('\n');
            j++;
        }

        return sb.ToString();
    }

    private static List<string> Split(string text)
    {
        var unified = text.Replace("\r\n", "\n").Replace("\r", "\n");
        if (unified.Length == 0)
        {
            return new List<string>();
        }

        return unified.TrimEnd('\n').Split('\n').ToList();
    }

    private static List<string> LongestCommonSubsequence(List<string> a, List<string> b)
    {
        var dp = new int[a.Count + 1, b.Count + 1];
        for (var i = a.Count - 1; i >= 0; i--)
        {
            for (var j = b.Count - 1; j >= 0; j--)
            {
                dp[i, j] = a[i] == b[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        var result = new List<string>();
        int x = 0, y = 0;
        while (x < a.Count && y < b.Count)
        {
            if (a[x] == b[y])
            {
                result.Add(a[x]);
                x++;
                y++;
            }
            else if (dp[x + 1, y] >= dp[x, y + 1])
            {
                x++;
            }
            else
            {
                y++;
            }
        }

        return result;
    }
}
