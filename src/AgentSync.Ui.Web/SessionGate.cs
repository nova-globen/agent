namespace AgentSync.Ui.Web;

/// <summary>How a request should be treated by the session gate.</summary>
public enum SessionDecision
{
    /// <summary>No valid token was presented; reject with 401.</summary>
    Deny,

    /// <summary>A valid session cookie is present; serve the request.</summary>
    AllowFromCookie,

    /// <summary>
    /// A valid <c>?token=</c> query was presented; the caller should set the session cookie
    /// and redirect to the same path with the token stripped from the URL.
    /// </summary>
    AllowFromQuery,
}

/// <summary>
/// Pure session-gate logic, kept free of ASP.NET types so it is unit-testable. Every
/// request must present the per-launch session token: first as a <c>?token=</c> query
/// (from the URL the CLI opened), which is then exchanged into an HttpOnly,
/// <c>SameSite=Strict</c> cookie and stripped from the address bar. A host with no
/// configured token denies everything. The readiness endpoint is the only public path.
/// </summary>
public static class SessionGate
{
    public const string CookieName = "agent_sync_token";
    public const string QueryName = "token";

    /// <summary>Unauthenticated paths (readiness only — they expose no repository data).</summary>
    public static bool IsPublicPath(string path)
        => string.Equals(path, "/healthz", StringComparison.Ordinal);

    /// <summary>
    /// Decides how to treat a request given the configured token and the token values that
    /// arrived via cookie and query. Cookie is preferred so an already-authenticated session
    /// never triggers another redirect.
    /// </summary>
    public static SessionDecision Decide(string expectedToken, string? cookieToken, string? queryToken)
    {
        if (string.IsNullOrEmpty(expectedToken))
        {
            return SessionDecision.Deny;
        }

        if (TokenCheck.Matches(expectedToken, cookieToken))
        {
            return SessionDecision.AllowFromCookie;
        }

        if (TokenCheck.Matches(expectedToken, queryToken))
        {
            return SessionDecision.AllowFromQuery;
        }

        return SessionDecision.Deny;
    }

    /// <summary>
    /// Builds the redirect location for a query-authenticated request: the same path with the
    /// <c>token</c> query parameter removed, preserving any other query parameters.
    /// </summary>
    public static string RedirectWithoutToken(string path, IEnumerable<KeyValuePair<string, string?>> query)
    {
        var kept = query
            .Where(kv => !string.Equals(kv.Key, QueryName, StringComparison.Ordinal))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value ?? string.Empty)}")
            .ToList();

        var basePath = string.IsNullOrEmpty(path) ? "/" : path;
        return kept.Count == 0 ? basePath : $"{basePath}?{string.Join('&', kept)}";
    }
}
