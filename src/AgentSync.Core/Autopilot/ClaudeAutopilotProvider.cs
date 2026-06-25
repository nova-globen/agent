using System.Diagnostics;
using System.Text;

namespace AgentSync.Core.Autopilot;

/// <summary>
/// Drives a headless <c>claude</c> CLI session and extracts a structured verdict using a
/// second headless invocation to parse the previous session's output.
/// </summary>
public sealed class ClaudeAutopilotProvider : IAutopilotProvider
{
    public string Name => "claude";

    public bool IsAvailable()
    {
        // On Windows, cmd.exe is the host so we find claude regardless of whether it is a
        // .exe, .cmd, or .ps1 wrapper (PATHEXT is respected by cmd.exe but not by CreateProcess).
        try
        {
            var psi = BuildVersionCheckPsi();
            using var p = Process.Start(psi);
            if (p is null)
            {
                return false;
            }

            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Runs <c>claude --dangerously-skip-permissions -p "continue autopilot"</c>, streams each
    /// output line to <paramref name="consoleOut"/>, and returns the full captured output.
    /// </summary>
    public async Task<string> RunSessionAsync(TextWriter consoleOut, CancellationToken ct)
    {
        var psi = BuildPsi();
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("continue autopilot");

        return await RunAndStreamAsync(psi, consoleOut, ct);
    }

    /// <summary>
    /// Passes <paramref name="sessionOutput"/> plus a JSON-extraction instruction to Claude
    /// headlessly (via stdin) and returns a parsed <see cref="AutopilotResult"/>.
    /// </summary>
    public async Task<AutopilotResult> ParseResultAsync(string sessionOutput, CancellationToken ct)
    {
        var prompt = BuildParsePrompt(sessionOutput);

        var psi = BuildPsi();
        psi.RedirectStandardInput = true;

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start 'claude'.");

        // Write prompt to stdin and close the stream concurrently with reading stdout.
        var writeTask = Task.Run(async () =>
        {
            await process.StandardInput.WriteAsync(prompt.AsMemory(), ct);
            process.StandardInput.Close();
        }, ct);

        var readTask = process.StandardOutput.ReadToEndAsync(ct);

        await Task.WhenAll(writeTask, readTask);
        await process.WaitForExitAsync(ct);

        var raw = await readTask;
        return AutopilotResultParser.Parse(raw.Trim());
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a PSI suitable for a claude version check (no stream redirection needed).
    /// On Windows uses <c>cmd.exe /c</c> so PATHEXT is honoured (finds .exe, .cmd, .ps1 wrappers).
    /// </summary>
    private static ProcessStartInfo BuildVersionCheckPsi()
    {
        var psi = StartWithShell("--version");
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        return psi;
    }

    /// <summary>
    /// Builds a PSI to invoke claude with <c>--dangerously-skip-permissions</c> and stream
    /// redirection. Caller adds any extra arguments after construction.
    /// </summary>
    private static ProcessStartInfo BuildPsi()
    {
        var psi = StartWithShell("--dangerously-skip-permissions");
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        return psi;
    }

    /// <summary>
    /// Creates a <see cref="ProcessStartInfo"/> that runs <c>claude [args]</c> in a way that
    /// works on both Unix (direct exec) and Windows (via <c>cmd.exe /c</c> to honour PATHEXT).
    /// </summary>
    private static ProcessStartInfo StartWithShell(params string[] claudeArgs)
    {
        if (OperatingSystem.IsWindows())
        {
            // cmd.exe /c resolves claude through PATHEXT, finding .exe, .cmd, or .ps1 wrappers.
            var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("claude");
            foreach (var a in claudeArgs)
            {
                psi.ArgumentList.Add(a);
            }

            return psi;
        }
        else
        {
            var psi = new ProcessStartInfo("claude") { UseShellExecute = false };
            foreach (var a in claudeArgs)
            {
                psi.ArgumentList.Add(a);
            }

            return psi;
        }
    }

    private static async Task<string> RunAndStreamAsync(
        ProcessStartInfo psi,
        TextWriter consoleOut,
        CancellationToken ct)
    {
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start 'claude'.");

        var capture = new StringBuilder();

        // Register cancellation: kill the process if the token is cancelled.
        await using var reg = ct.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch { }
        });

        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync(ct)) is not null)
        {
            consoleOut.WriteLine(line);
            capture.AppendLine(line);
        }

        await process.WaitForExitAsync(ct);
        return capture.ToString();
    }

    private static string BuildParsePrompt(string sessionOutput)
    {
        // Build without raw-string-literal interpolation to avoid brace-escaping issues.
        return
            "The following text is the full output of a headless AI-agent CLI session:\n\n" +
            "---BEGIN SESSION OUTPUT---\n" +
            sessionOutput + "\n" +
            "---END SESSION OUTPUT---\n\n" +
            "Analyze the output above and respond ONLY with a JSON object matching this schema:\n\n" +
            "{\n" +
            "  \"failed\": <boolean>,\n" +
            "  \"done\": <boolean>,\n" +
            "  \"message\": \"<one-paragraph markdown summary>\",\n" +
            "  \"retry\": { \"afterSeconds\": <integer> }\n" +
            "}\n\n" +
            "Rules:\n" +
            "- \"failed\" is true when the session ended with an error, network failure, usage-limit hit,\n" +
            "  or an unrecoverable blocker that is not expected to resolve by itself.\n" +
            "- \"done\" is true when the loop should stop entirely: all planned work is complete\n" +
            "  (failed=false) OR there is a hard blocker the user must resolve (failed=true, no retry).\n" +
            "- \"retry\" must be included (and \"done\" must be false) when the failure is transient and\n" +
            "  retrying after a delay will likely succeed — e.g. an API usage-limit reset.\n" +
            "  Set \"afterSeconds\" to the number of seconds to wait (e.g. 3600 for a 1-hour limit).\n" +
            "  Omit \"retry\" entirely when no retry is appropriate.\n" +
            "- \"message\" is a concise plain-text/markdown paragraph summarising what happened and\n" +
            "  what the next session should do (if anything).\n\n" +
            "Respond with ONLY the JSON object. No markdown fences. No prose before or after it.";
    }

}
