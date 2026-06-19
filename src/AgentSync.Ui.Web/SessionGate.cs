using Microsoft.AspNetCore.Http;

namespace AgentSync.Ui.Web;

/// <summary>
/// Gates every request on the short-lived session token. The token may arrive as a
/// <c>?token=</c> query parameter (first navigation, from the URL the CLI printed); on a
/// match it is stored in an HttpOnly, SameSite=Strict cookie for subsequent navigations.
/// A host with no configured token denies everything.
/// </summary>
public static class SessionGate
{
    public const string CookieName = "agent_sync_token";
    public const string QueryName = "token";

    public static bool Authorize(HttpContext context, string expectedToken)
    {
        if (string.IsNullOrEmpty(expectedToken))
        {
            return false;
        }

        if (context.Request.Cookies.TryGetValue(CookieName, out var cookie) && TokenCheck.Matches(expectedToken, cookie))
        {
            return true;
        }

        if (context.Request.Query.TryGetValue(QueryName, out var query) && TokenCheck.Matches(expectedToken, query.ToString()))
        {
            context.Response.Cookies.Append(CookieName, expectedToken, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = false, // loopback http only
                IsEssential = true,
            });
            return true;
        }

        return false;
    }
}
