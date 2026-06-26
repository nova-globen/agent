using System.Diagnostics;
using System.Text.Json;

namespace AgentSync.Core.Autopilot;

/// <summary>
/// Drives a headless <c>codex exec</c> session (OpenAI Codex CLI) and extracts a
/// structured verdict by running a second invocation to inspect the repository state.
/// </summary>
/// <remarks>
/// Key characteristics versus <see cref="ClaudeAutopilotProvider"/>:
/// <list type="bullet">
///   <item>Prompt is a positional argument: <c>codex exec "continue autopilot"</c> (not <c>-p</c>).</item>
///   <item>Headless / no-approval mode uses <c>--sandbox workspace-write</c>; the stricter
///   <c>--dangerously-bypass-approvals-and-sandbox</c> (<c>--yolo</c>) is available when the
///   operator needs full FS access.</item>
///   <item>Streaming events: <c>--json</c> produces newline-delimited JSON. Content extracted
///   from the <c>"content"</c> / <c>"text"</c> field when present; raw line used otherwise.</item>
///   <item>Session resume: <c>codex exec resume &lt;session_id&gt;</c> (separate positional, not
///   a flag like Claude's <c>--resume</c>). The session_id is captured from the first JSON event
///   that contains a <c>"session_id"</c> field.</item>
///   <item>No <c>--json-schema</c> equivalent — the parse step requests JSON in the prompt
///   and uses <see cref="AutopilotResultParser.Parse"/> (markdown-strip path).</item>
///   <item>Model: <c>--model best</c> is passed to all invocations (override via constructor).</item>
/// </list>
/// Authentication: configure via <c>codex login</c> or set <c>OPENAI_API_KEY</c>.
/// </remarks>
public sealed class CodexAutopilotProvider : IAutopilotProvider
{
    private readonly string? _model;

    /// <param name="model">Model override passed via <c>--model</c>. Defaults to <c>"best"</c>.</param>
    public CodexAutopilotProvider(string? model = "best") => _model = model;

    public string Name => "codex";

    public bool IsAvailable()
    {
        try
        {
            var psi = BuildPsi("--version");
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
    /// Runs one headless <c>codex exec</c> session.
    /// <para>
    /// When <paramref name="observer"/> is non-null, adds <c>--json</c> and streams
    /// newline-delimited JSON events as text deltas. Token counts are not exposed by the
    /// Codex CLI and are reported as zero. The session_id is captured from the first JSON
    /// event containing a <c>"session_id"</c> field.
    /// </para>
    /// <para>
    /// When <paramref name="observer"/> is null (headless/CI), stdout flows directly to
    /// the terminal with no capture.
    /// </para>
    /// <para>
    /// Pass a <paramref name="resumeSessionId"/> to call <c>codex exec resume &lt;id&gt;</c>
    /// instead of a fresh session.
    /// </para>
    /// </summary>
    public async Task<string?> RunSessionAsync(
        IAutopilotSessionObserver? observer,
        string? resumeSessionId,
        CancellationToken ct)
    {
        if (observer is null)
        {
            // Headless/CI: stdout goes to the terminal.
            var psi = BuildSessionPsi(resumeSessionId, json: false);
            psi.RedirectStandardInput = true;

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start 'codex'.");

            process.StandardInput.Close();

            await using var reg = ct.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            });

            await process.WaitForExitAsync(ct);
            return null;
        }

        // Observer path: redirect stdout, parse NDJSON, forward as text deltas.
        var streamPsi = BuildSessionPsi(resumeSessionId, json: true);
        streamPsi.RedirectStandardInput  = true;
        streamPsi.RedirectStandardOutput = true;
        streamPsi.RedirectStandardError  = true;

        using var streamProcess = Process.Start(streamPsi)
            ?? throw new InvalidOperationException("Failed to start 'codex'.");

        streamProcess.StandardInput.Close();

        var stderrTask = streamProcess.StandardError.ReadToEndAsync(ct);
        var sw         = Stopwatch.StartNew();
        string? sessionId = null;

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

                // Try to extract structured content from the NDJSON event.
                var text = ExtractTextFromEvent(line, ref sessionId) ?? (line + "\n");
                observer.OnTextDelta(text);
                // Codex CLI does not expose token counts; report elapsed only.
                observer.OnStats(EmptyStats(sw.Elapsed));
            }

            await streamProcess.WaitForExitAsync(ct);
        }
        finally
        {
            try { await stderrTask; } catch { }
            observer.OnStats(EmptyStats(sw.Elapsed));
        }

        return sessionId;
    }

    /// <summary>
    /// Asks Codex to inspect the repository state and return a structured verdict.
    /// Since the Codex CLI has no <c>--json-schema</c> equivalent, JSON is requested
    /// via the prompt text and parsed with <see cref="AutopilotResultParser.Parse"/>
    /// (handles plain and markdown-fenced JSON).
    /// </summary>
    public async Task<AutopilotResult> ParseResultAsync(string sessionOutput, CancellationToken ct)
    {
        var psi = BuildPsi("exec", "--sandbox", "read-only", BuildParsePrompt());
        if (_model is not null)
        {
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(_model);
        }

        psi.UseShellExecute      = false;
        psi.RedirectStandardInput  = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError  = true;

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start 'codex'.");

        process.StandardInput.Close();

        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        var raw        = await process.StandardOutput.ReadToEndAsync(ct);
        await stderrTask;
        await process.WaitForExitAsync(ct);

        return AutopilotResultParser.Parse(raw.Trim());
    }

    // -------------------------------------------------------------------------

    private ProcessStartInfo BuildSessionPsi(string? resumeSessionId, bool json)
    {
        ProcessStartInfo psi;

        if (resumeSessionId is not null)
        {
            // codex exec resume <session_id> [--json] [--model <m>] [--sandbox <policy>]
            psi = BuildPsi("exec", "resume", resumeSessionId);
        }
        else
        {
            // codex exec "continue autopilot" [--json] [--model <m>] [--sandbox <policy>]
            psi = BuildPsi("exec", "continue autopilot");
        }

        psi.ArgumentList.Add("--sandbox");
        psi.ArgumentList.Add("workspace-write");

        if (_model is not null)
        {
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(_model);
        }

        if (json)
            psi.ArgumentList.Add("--json");

        psi.UseShellExecute = false;
        return psi;
    }

    private static ProcessStartInfo BuildPsi(params string[] args)
    {
        var psi = new ProcessStartInfo("codex") { UseShellExecute = false };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        return psi;
    }

    /// <summary>
    /// Attempts to extract human-readable text from a Codex NDJSON event line.
    /// Also captures the <c>session_id</c> from the first event that contains it.
    /// Returns <c>null</c> when the line should be forwarded as-is.
    /// </summary>
    private static string? ExtractTextFromEvent(string line, ref string? sessionId)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            // Capture session_id from any event that carries it.
            if (sessionId is null &&
                root.TryGetProperty("session_id", out var sid) &&
                sid.ValueKind == JsonValueKind.String)
            {
                sessionId = sid.GetString();
            }

            // Extract text content — handle both string and array shapes.
            if (root.TryGetProperty("content", out var content))
            {
                if (content.ValueKind == JsonValueKind.String)
                    return content.GetString() + "\n";

                if (content.ValueKind == JsonValueKind.Array)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var item in content.EnumerateArray())
                    {
                        if (item.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                            sb.Append(t.GetString());
                        else if (item.ValueKind == JsonValueKind.String)
                            sb.Append(item.GetString());
                    }

                    if (sb.Length > 0) return sb.Append('\n').ToString();
                }
            }

            // Fallback: plain "text" field.
            if (root.TryGetProperty("text", out var textProp) &&
                textProp.ValueKind == JsonValueKind.String)
            {
                return textProp.GetString() + "\n";
            }

            // Event carries no visible text (e.g. a tool-call or status event).
            return null;
        }
        catch (JsonException)
        {
            return null;  // not JSON — let the caller forward the raw line
        }
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
