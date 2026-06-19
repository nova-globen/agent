using System.Security.Cryptography;
using System.Text;

namespace AgentSync.Ui.Web;

/// <summary>Fixed-time comparison of the session token. An empty expected token never matches.</summary>
public static class TokenCheck
{
    public static bool Matches(string expected, string? provided)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided))
        {
            return false;
        }

        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(provided);
        if (a.Length != b.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
