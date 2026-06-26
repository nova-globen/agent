using System.Diagnostics;

namespace AgentSync.Core.Autopilot;

/// <summary>
/// Drives a headless <c>copilot</c> CLI session (GitHub Copilot CLI) and extracts a
/// structured verdict by running a second invocation to inspect the repository state.
/// </summary>
/// <remarks>
/// Key differences from <see cref="ClaudeAutopilotProvider"/>:
/// <list type="bullet">
///   <item>Uses <c>-p</c>, <c>-s</c>, <c>--allow-all</c>, <c>--no-ask-user</c> flags.</item>
///   <item>No <c>--output-format stream-json</c> equivalent — output is plain text.
///   When an observer is supplied, lines arrive as text deltas but token counts are
///   unavailable (reported as zero). Output may be block-buffered rather than
///   line-buffered when stdout is a pipe.</item>
///   <item>No <c>--json-schema</c> equivalent — the parse step asks for JSON in the
///   prompt text and uses <see cref="AutopilotResultParser.Parse"/> (markdown-strip path).</item>
///   <item>No <c>--resume</c> equivalent — <c>resumeSessionId</c> is accepted but not used;
///   <c>RunSessionAsync</c> always returns <c>null</c>.</item>
/// </list>
/// Authentication: set <c>COPILOT_GITHUB_TOKEN</c>, <c>GH_TOKEN</c>, or <c>GITHUB_TOKEN</c>.
/// </remarks>
public sealed class CopilotAutopilotProvider : IAutopilotProvider
{
    public string Name => "copilot";

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

    /// <summary>
    /// Runs <c>copilot -p "continue autopilot" -s --allow-all --no-ask-user</c>.
    /// <para>
    /// When <paramref name="observer"/> is non-null, stdout is redirected and lines
    /// are forwarded as <see cref="IAutopilotSessionObserver.OnTextDelta"/> calls.
    /// Token counts in <see cref="AutopilotSessionStats"/> are zero (not exposed by
    /// the Copilot CLI); only elapsed time is tracked.
    /// </para>
    /// <para>
    /// When <paramref name="observer"/> is null (headless/CI), stdout flows directly
    /// to the terminal.
    /// </para>
    /// <para>
    /// <paramref name="resumeSessionId"/> is accepted for interface compatibility but
    /// has no effect — the Copilot CLI has no <c>--resume</c> equivalent.
    /// Always returns <c>null</c>.
    /// </para>
    /// </summary>
    public async Task<string?> RunSessionAsync(
        IAutopilotSessionObserver? observer,
        string? resumeSessionId,
        CancellationToken ct)
    {
        if (observer is null)
        {
            // Headless/CI: let output go to the terminal.
            var psi = BuildSessionPsi();
            psi.RedirectStandardInput = true;

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start 'copilot'.");

            process.StandardInput.Close();

            await using var reg = ct.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            });

            await process.WaitForExitAsync(ct);
            return null;
        }

        // Observer path: redirect stdout, forward lines as text deltas.
        var streamPsi = BuildSessionPsi();
        streamPsi.RedirectStandardInput = true;
        streamPsi.RedirectStandardOutput = true;
        streamPsi.RedirectStandardError = true;

        using var streamProcess = Process.Start(streamPsi)
            ?? throw new InvalidOperationException("Failed to start 'copilot'.");

        streamProcess.StandardInput.Close();

        var stderrTask = streamProcess.StandardError.ReadToEndAsync(ct);
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
                observer.OnTextDelta(line + "\n");
                // Copilot CLI does not expose token counts or cost; report elapsed only.
                observer.OnStats(EmptyStats(sw.Elapsed));
            }

            await streamProcess.WaitForExitAsync(ct);
        }
        finally
        {
            try { await stderrTask; } catch { }
            observer.OnStats(EmptyStats(sw.Elapsed));
        }

        return null; // Copilot CLI does not expose a session_id
    }

    /// <summary>
    /// Asks Copilot to inspect the repository state and return a structured verdict.
    /// Since the Copilot CLI has no <c>--json-schema</c> flag, the JSON is requested
    /// in the prompt text and parsed via <see cref="AutopilotResultParser.Parse"/>
    /// (handles plain JSON and markdown-fenced JSON).
    /// </summary>
    public async Task<AutopilotResult> ParseResultAsync(string sessionOutput, CancellationToken ct)
    {
        var psi = StartDirect("-p", BuildParsePrompt(), "-s", "--allow-all", "--no-ask-user");
        psi.UseShellExecute = false;
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start 'copilot'.");

        process.StandardInput.Close();

        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        var raw = await process.StandardOutput.ReadToEndAsync(ct);
        await stderrTask;
        await process.WaitForExitAsync(ct);

        return AutopilotResultParser.Parse(raw.Trim());
    }

    // -------------------------------------------------------------------------

    private static ProcessStartInfo BuildSessionPsi() =>
        StartDirect("-p", "continue autopilot", "-s", "--allow-all", "--no-ask-user");

    private static ProcessStartInfo StartDirect(params string[] args)
    {
        var psi = new ProcessStartInfo("copilot") { UseShellExecute = false };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        return psi;
    }

    private static AutopilotSessionStats EmptyStats(TimeSpan elapsed) =>
        new(InputTokens: 0, CacheReadTokens: 0, CacheCreationTokens: 0,
            OutputTokens: 0, ToolCallCount: 0, RateLimitHits: 0,
            CostUsd: 0m, Elapsed: elapsed);

    private static string BuildParsePrompt() =>
        "A headless autopilot session just completed in this repository. " +
        "Determine the outcome by examining the current repo state:\n" +
        "1. Run: git log --oneline -5\n" +
        "2. List .agent/prompts/autopilot/ and read the newest prompt-*.txt file (if any).\n\n" +
        "Respond with ONLY a JSON object — no markdown fences, no prose before or after:\n" +
        "{\"failed\":<bool>,\"done\":<bool>,\"message\":\"<one-paragraph summary>\",\"retry\":{\"afterSeconds\":<int>}}\n\n" +
        "Rules:\n" +
        "- failed=true if error, hard blocker, or usage-limit hit.\n" +
        "- done=true if all planned work is complete OR unrecoverable blocker.\n" +
        "- retry.afterSeconds > 0 only for transient failures (e.g. rate limit); 0 otherwise.\n" +
        "- If a new handoff prompt exists, the session completed work: done=false, failed=false.";
}
