using System.Diagnostics;
using System.Text.RegularExpressions;

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
            var psi = StartDirect("--version");
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

    private static readonly Regex AnsiEscape = new(@"\x1B\[[0-9;]*[mK]", RegexOptions.Compiled);

    /// <summary>
    /// Runs one headless claude session and returns the <c>session_id</c> assigned by claude.
    /// <para>
    /// When <paramref name="observer"/> is non-null, uses <c>--output-format stream-json</c>
    /// to stream NDJSON lines, fires typed events, and captures the session_id from the
    /// <c>system/init</c> envelope. When <paramref name="observer"/> is null (headless/CI),
    /// stdout flows directly to the terminal and the session_id is not captured (returns null).
    /// </para>
    /// <para>
    /// Pass a <paramref name="resumeSessionId"/> to append <c>--resume &lt;id&gt;</c> and
    /// continue an existing conversation rather than starting a fresh one. Works in both modes.
    /// </para>
    /// </summary>
    public async Task<string?> RunSessionAsync(
        IAutopilotSessionObserver? observer,
        string? resumeSessionId,
        CancellationToken ct)
    {
        if (observer is null)
        {
            // Headless/CI: no capture — claude writes raw to the terminal.
            var headlessPsi = StartDirect("--dangerously-skip-permissions", "-p", "continue autopilot");
            if (resumeSessionId is not null)
            {
                headlessPsi.ArgumentList.Add("--resume");
                headlessPsi.ArgumentList.Add(resumeSessionId);
            }
            headlessPsi.UseShellExecute = false;
            headlessPsi.RedirectStandardInput = true;

            using var headlessProcess = Process.Start(headlessPsi)
                ?? throw new InvalidOperationException("Failed to start 'claude'.");

            // Close stdin = /dev/null: immediate EOF; avoids 3-second wait and prevents
            // claude from launching its interactive TUI. cmd.exe /c is NOT used because
            // it caused spurious CTRL+C signals propagating back to the parent process.
            headlessProcess.StandardInput.Close();

            await using var headlessReg = ct.Register(() =>
            {
                try { headlessProcess.Kill(entireProcessTree: true); } catch { }
            });

            await headlessProcess.WaitForExitAsync(ct);
            return null;  // session_id not captured in headless mode
        }

        // Streaming mode: parse NDJSON, fire observer events, capture session_id.
        var streamPsi = StartDirect(
            "--dangerously-skip-permissions",
            "--output-format", "stream-json",
            "--verbose",
            "--include-partial-messages",
            "-p", "continue autopilot");
        if (resumeSessionId is not null)
        {
            streamPsi.ArgumentList.Add("--resume");
            streamPsi.ArgumentList.Add(resumeSessionId);
        }
        streamPsi.UseShellExecute = false;
        streamPsi.RedirectStandardInput = true;
        streamPsi.RedirectStandardOutput = true;
        streamPsi.RedirectStandardError = true;

        using var streamProcess = Process.Start(streamPsi)
            ?? throw new InvalidOperationException("Failed to start 'claude'.");

        streamProcess.StandardInput.Close();

        // Drain stderr concurrently to avoid pipe-full deadlock.
        var stderrTask = streamProcess.StandardError.ReadToEndAsync(ct);

        var pendingTools = new Dictionary<string, string>();
        var stats = new ClaudeStreamParser.Stats();
        var sw = Stopwatch.StartNew();

        await using var streamReg = ct.Register(() =>
        {
            try { streamProcess.Kill(entireProcessTree: true); } catch { }
        });

        try
        {
            string? line;
            while ((line = await streamProcess.StandardOutput.ReadLineAsync(ct)) is not null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                ClaudeStreamParser.DispatchLine(line, stats, observer, pendingTools);
                observer.OnStats(stats.Snapshot(sw.Elapsed));
            }

            await streamProcess.WaitForExitAsync(ct);
        }
        finally
        {
            try { await stderrTask; } catch { }
            observer.OnStats(stats.Snapshot(sw.Elapsed));
        }

        return stats.SessionId;
    }

    /// <summary>
    /// Asks Claude to inspect the repository state (recent commits and autopilot handoff
    /// files) and return a structured <see cref="AutopilotResult"/> verdict.
    /// Uses <c>--output-format json --json-schema</c> so the verdict lands in the
    /// <c>structured_output</c> field of the result envelope — no markdown stripping needed.
    /// </summary>
    public async Task<AutopilotResult> ParseResultAsync(string sessionOutput, CancellationToken ct)
    {
        var psi = StartDirect(
            "--dangerously-skip-permissions",
            "--output-format", "json",
            "--json-schema", ParseSchema,
            "-p", BuildParsePrompt());

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

        return AutopilotResultParser.ParseFromEnvelope(raw.Trim());
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a <see cref="ProcessStartInfo"/> for <c>claude [args]</c>.
    /// Runs <c>claude</c> (or <c>claude.exe</c>) directly without a shell intermediary.
    /// On Windows, <c>CreateProcess</c> appends <c>.exe</c> when the name has no extension,
    /// so <c>claude.exe</c> is found on PATH. cmd.exe /c is intentionally avoided: it
    /// generates spurious CTRL+C signals back to the parent process on exit.
    /// </summary>
    private static ProcessStartInfo StartDirect(params string[] claudeArgs)
    {
        var psi = new ProcessStartInfo("claude");
        foreach (var a in claudeArgs)
        {
            psi.ArgumentList.Add(a);
        }

        return psi;
    }

    /// <summary>
    /// Builds the prompt for the parse step. The JSON schema is enforced by
    /// <c>--json-schema</c>, so the prompt only needs to describe the task — no
    /// "respond ONLY with JSON" instructions required.
    /// </summary>
    private static string BuildParsePrompt() =>
        "A headless autopilot session just completed in this repository. " +
        "Determine the outcome by examining the current repo state:\n" +
        "1. Run: git log --oneline -5\n" +
        "2. List .agent/prompts/autopilot/ and read the newest prompt-*.txt file (if any).\n\n" +
        "Report the verdict: whether the session failed, whether the autopilot is done, " +
        "a one-paragraph summary of what happened and what comes next, and how many " +
        "seconds to wait before retrying (0 if no retry is needed).\n\n" +
        "Rules:\n" +
        "- failed=true if the session ended with an error, hard blocker, or usage-limit hit.\n" +
        "- done=true if all planned work is complete OR if there is an unrecoverable blocker.\n" +
        "- retry.afterSeconds > 0 only for transient failures (e.g. usage-limit reset).\n" +
        "- If a new handoff prompt exists, the session completed work; set done=false, failed=false.";

    /// <summary>
    /// JSON Schema for the parse-step verdict. Passed via <c>--json-schema</c>.
    /// Constraints that the grammar cannot enforce (non-empty string, afterSeconds >= 0)
    /// are restated in field descriptions and re-validated by the parser.
    /// </summary>
    private const string ParseSchema =
        """
        {"type":"object","additionalProperties":false,"required":["failed","done","message","retry"],"properties":{"failed":{"type":"boolean","description":"Whether the session ended with an error, hard blocker, or usage-limit hit."},"done":{"type":"boolean","description":"Whether the loop should stop: all work is complete, or there is an unrecoverable blocker."},"message":{"type":"string","description":"One-paragraph summary of what happened and what comes next. Must be non-empty."},"retry":{"type":"object","additionalProperties":false,"required":["afterSeconds"],"properties":{"afterSeconds":{"type":"integer","description":"Seconds to wait before retrying. Use 0 when no retry is needed (>= 0)."}}}}}
        """;
}
