namespace AgentSync.Ui.Web;

/// <summary>
/// Parsed command-line options for the local web UI host, passed by the CLI's
/// <c>agent ui</c> launcher: <c>--repo &lt;path&gt; --port &lt;port&gt; --token &lt;token&gt;</c>
/// (plus an optional <c>--no-open</c>).
/// </summary>
public sealed record WebOptions(string Repo, int Port, string Token, bool NoOpen)
{
    public static WebOptions Parse(string[] args)
    {
        string? repo = null;
        var port = 0;
        string? token = null;
        var noOpen = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--repo":
                    repo = Next(args, ref i);
                    break;
                case "--port":
                    if (int.TryParse(Next(args, ref i), out var p))
                    {
                        port = p;
                    }

                    break;
                case "--token":
                    token = Next(args, ref i);
                    break;
                case "--no-open":
                    noOpen = true;
                    break;
            }
        }

        return new WebOptions(
            repo ?? Directory.GetCurrentDirectory(),
            port,
            token ?? string.Empty,
            noOpen);
    }

    private static string? Next(string[] args, ref int i)
        => i + 1 < args.Length ? args[++i] : null;
}
