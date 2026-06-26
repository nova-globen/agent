using System.Diagnostics;

namespace AgentSync.Core.Autopilot;

/// <summary>
/// Drives a headless <c>claude</c> CLI session and extracts a structured verdict by asking
/// a second Claude invocation to inspect the repository state.
/// </summary>
public sealed class ClaudeAutopilotProvider : IAutopilotProvider
{
    public string Name => "claude";

    public bool IsAvailable()
    {
        try
        {
            var psi = StartWithShell("--version");
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = true;
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Runs <c>claude --dangerously-skip-permissions -p "continue autopilot"</c>.
    /// Stdout and stderr are NOT redirected — claude writes directly to the user's terminal
    /// so the full session output is visible in real-time. Only stdin is redirected (and
    /// immediately closed) to signal non-interactive mode without attaching a TTY.
    /// Returns an empty string; the parse step inspects the repository state for the verdict.
    /// </summary>
    public async Task<string> RunSessionAsync(TextWriter consoleOut, CancellationToken ct)
    {
        var psi = StartWithShell("--dangerously-skip-permissions", "-p", "continue autopilot");
        psi.UseShellExecute = false;
        psi.RedirectStandardInput = true;
        // Stdout/stderr intentionally NOT redirected: claude writes directly to the terminal.

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start 'claude'.");

        // Close stdin immediately: this is the /dev/null equivalent that claude's own
        // warning message recommends ("redirect stdin explicitly: < /dev/null to skip").
        // EOF on stdin tells claude it is non-interactive without a 3-second wait.
        // cmd.exe /c is NOT used as intermediary (see StartWithShell) because it was
        // the source of spurious CTRL+C signals propagating back to our process.
        process.StandardInput.Close();

        await using var reg = ct.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch { }
        });

        await process.WaitForExitAsync(ct);
        return string.Empty;
    }

    /// <summary>
    /// Asks Claude to inspect the repository state (recent commits and autopilot handoff
    /// files) and return a structured <see cref="AutopilotResult"/> verdict.
    /// The prompt is passed via <c>-p</c>; stdout is redirected to capture the JSON response.
    /// </summary>
    public async Task<AutopilotResult> ParseResultAsync(string sessionOutput, CancellationToken ct)
    {
        var prompt = BuildParsePrompt();

        var psi = StartWithShell("--dangerously-skip-permissions", "-p", prompt);
        psi.UseShellExecute = false;
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start 'claude'.");

        process.StandardInput.Close();

        // Read stderr concurrently to prevent pipe-full deadlock.
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        var raw = await process.StandardOutput.ReadToEndAsync(ct);
        await stderrTask;
        await process.WaitForExitAsync(ct);

        return AutopilotResultParser.Parse(raw.Trim());
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a <see cref="ProcessStartInfo"/> for <c>claude [args]</c>.
    /// Runs <c>claude</c> (or <c>claude.exe</c>) directly without a shell intermediary.
    /// On Windows, <c>CreateProcess</c> appends <c>.exe</c> when the name has no extension,
    /// so <c>claude.exe</c> is found on PATH. cmd.exe /c is intentionally avoided: it
    /// generates spurious CTRL+C signals back to the parent process on exit.
    /// </summary>
    private static ProcessStartInfo StartWithShell(params string[] claudeArgs)
    {
        var psi = new ProcessStartInfo("claude");
        foreach (var a in claudeArgs)
        {
            psi.ArgumentList.Add(a);
        }

        return psi;
    }

    /// <summary>
    /// Builds a compact prompt (passed via <c>-p</c>) that asks Claude to inspect the
    /// repository state and return a JSON verdict. Does not reference captured session output
    /// because the session runs with stdout un-redirected (output goes to the terminal).
    /// </summary>
    private static string BuildParsePrompt()
    {
        return
            "A headless autopilot session just completed in this repository. " +
            "Determine the outcome by examining the current repo state:\n" +
            "1. Run: git log --oneline -5\n" +
            "2. List .agent/prompts/autopilot/ and read the newest prompt-*.txt file (if any).\n" +
            "Then respond ONLY with a JSON object:\n" +
            "{\n" +
            "  \"failed\": <boolean>,\n" +
            "  \"done\": <boolean>,\n" +
            "  \"message\": \"<one-paragraph summary of what happened and what comes next>\",\n" +
            "  \"retry\": { \"afterSeconds\": <integer> }\n" +
            "}\n" +
            "Rules: " +
            "failed=true if the session ended with an error, hard blocker, or usage-limit hit. " +
            "done=true if all planned work is complete (no more increments left) OR if there is an unrecoverable blocker. " +
            "Include retry only when the failure is transient (e.g. usage-limit reset); set afterSeconds to the wait time. " +
            "If a new handoff prompt exists, the session completed work and the loop should continue (done=false, failed=false). " +
            "Respond with ONLY the JSON. No markdown fences. No prose before or after.";
    }
}
