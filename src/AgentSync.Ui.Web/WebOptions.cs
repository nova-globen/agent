namespace AgentSync.Ui.Web;

/// <summary>
/// Parsed command-line options for the local web UI host, passed by the CLI's
/// <c>agent ui</c> launcher: <c>--repo &lt;path&gt; --port &lt;port&gt; --token &lt;token&gt;</c>
/// (plus an optional <c>--no-open</c>). Parsing is strict: every required option must be
/// present and valid, unknown options are rejected, and the repository must exist.
/// </summary>
public sealed record WebOptions(string Repo, int Port, string Token, bool NoOpen)
{
    public const string Usage =
        "Usage: agent-sync-ui --repo <path> --port <port> --token <token> [--no-open]";

    private const int MinPort = 1;
    private const int MaxPort = 65535;

    /// <summary>
    /// Strictly parses the host options. Returns true on success; otherwise <paramref name="error"/>
    /// describes the first problem and <paramref name="options"/> is null. Validates that
    /// <c>--repo</c> exists, <c>--port</c> is a valid TCP port, and <c>--token</c> is non-empty.
    /// </summary>
    public static bool TryParse(string[] args, out WebOptions? options, out string? error)
    {
        options = null;
        error = null;

        string? repo = null;
        string? portText = null;
        string? token = null;
        var noOpen = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--repo":
                    if (!TryValue(args, ref i, arg, out repo, out error)) return false;
                    break;
                case "--port":
                    if (!TryValue(args, ref i, arg, out portText, out error)) return false;
                    break;
                case "--token":
                    if (!TryValue(args, ref i, arg, out token, out error)) return false;
                    break;
                case "--no-open":
                    noOpen = true;
                    break;
                default:
                    error = $"unknown option '{arg}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(repo))
        {
            error = "--repo is required.";
            return false;
        }

        if (!Directory.Exists(repo))
        {
            error = $"--repo path does not exist: {repo}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(portText))
        {
            error = "--port is required.";
            return false;
        }

        if (!int.TryParse(portText, out var port))
        {
            error = $"--port must be an integer: {portText}";
            return false;
        }

        if (port is < MinPort or > MaxPort)
        {
            error = $"--port must be between {MinPort} and {MaxPort}: {port}";
            return false;
        }

        if (string.IsNullOrEmpty(token))
        {
            error = "--token is required.";
            return false;
        }

        options = new WebOptions(repo, port, token, noOpen);
        return true;
    }

    private static bool TryValue(string[] args, ref int i, string option, out string? value, out string? error)
    {
        if (i + 1 < args.Length)
        {
            value = args[++i];
            error = null;
            return true;
        }

        value = null;
        error = $"option '{option}' requires a value.";
        return false;
    }
}
