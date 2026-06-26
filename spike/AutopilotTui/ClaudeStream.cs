using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutopilotTui;

/// <summary>
/// Runs claude with --output-format stream-json and raises typed events per NDJSON line.
/// </summary>
public sealed class ClaudeStream
{
    public event Action<string>? TextDelta;
    public event Action<string, string>? ToolStarted;   // (name, id)
    public event Action<string, bool>? ToolCompleted;   // (id, isError)
    public event Action<SessionStats>? StatsUpdated;
    public event Action<SessionStats>? SessionComplete;

    private static readonly Regex AnsiEscape = new(@"\x1B\[[0-9;]*[mK]", RegexOptions.Compiled);

    public async Task RunAsync(string prompt, string repoPath, SessionStats stats, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("claude")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = repoPath,
        };
        psi.ArgumentList.Add("--dangerously-skip-permissions");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("stream-json");
        psi.ArgumentList.Add("--verbose");
        psi.ArgumentList.Add("--include-partial-messages");
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(prompt);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start 'claude'.");

        process.StandardInput.Close();

        // Drain stderr in background to avoid pipe-full deadlock.
        var stderrTask = DrainAsync(process.StandardError, ct);

        var sw = Stopwatch.StartNew();
        stats.IsRunning = true;

        // Pending tool name lookup: id -> name
        var pendingTools = new Dictionary<string, string>();

        try
        {
            await using var reg = ct.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            });

            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(ct)) is not null)
            {
                stats.Elapsed = sw.Elapsed;
                if (string.IsNullOrWhiteSpace(line)) continue;

                try { DispatchLine(line, stats, pendingTools); }
                catch { /* malformed line — skip */ }

                StatsUpdated?.Invoke(stats);
            }

            await process.WaitForExitAsync(ct);
        }
        finally
        {
            stats.IsRunning = false;
            stats.Elapsed = sw.Elapsed;
            await stderrTask;
            SessionComplete?.Invoke(stats);
        }
    }

    private void DispatchLine(string line, SessionStats stats, Dictionary<string, string> pendingTools)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp)) return;
        var type = typeProp.GetString();

        switch (type)
        {
            case "rate_limit_event":
                if (root.TryGetProperty("rate_limit_info", out var rli) &&
                    rli.TryGetProperty("status", out var statusProp) &&
                    statusProp.GetString() is not "allowed")
                    stats.RateLimitHits++;
                break;

            case "stream_event":
                HandleStreamEvent(root, stats, pendingTools);
                break;

            case "system":
                HandleSystemEvent(root, stats);
                break;

            case "result":
                HandleResult(root, stats);
                break;
        }
    }

    private void HandleStreamEvent(JsonElement root, SessionStats stats, Dictionary<string, string> pendingTools)
    {
        if (!root.TryGetProperty("event", out var ev)) return;
        if (!ev.TryGetProperty("type", out var evTypeProp)) return;
        var evType = evTypeProp.GetString();

        switch (evType)
        {
            case "content_block_start":
                if (ev.TryGetProperty("content_block", out var cb) &&
                    cb.TryGetProperty("type", out var cbType) &&
                    cbType.GetString() == "tool_use")
                {
                    var id = cb.TryGetProperty("id", out var idp) ? idp.GetString() ?? "" : "";
                    var name = cb.TryGetProperty("name", out var np) ? np.GetString() ?? "tool" : "tool";
                    pendingTools[id] = name;
                    ToolStarted?.Invoke(name, id);
                    stats.ToolCallCount++;
                }
                break;

            case "content_block_delta":
                if (!ev.TryGetProperty("delta", out var delta)) break;
                if (!delta.TryGetProperty("type", out var deltaType)) break;
                if (deltaType.GetString() == "text_delta" &&
                    delta.TryGetProperty("text", out var textProp))
                {
                    TextDelta?.Invoke(textProp.GetString() ?? "");
                }
                break;

            case "message_delta":
                if (ev.TryGetProperty("usage", out var usage))
                    ApplyUsage(usage, stats);
                break;
        }
    }

    private void HandleSystemEvent(JsonElement root, SessionStats stats)
    {
        if (!root.TryGetProperty("subtype", out var sub)) return;
        switch (sub.GetString())
        {
            case "task_notification":
                if (root.TryGetProperty("tool_use_id", out var tid) &&
                    root.TryGetProperty("status", out var st))
                {
                    var id = tid.GetString() ?? "";
                    var isError = st.GetString() != "completed";
                    ToolCompleted?.Invoke(id, isError);
                }
                break;
        }
    }

    private static void HandleResult(JsonElement root, SessionStats stats)
    {
        stats.IsError = root.TryGetProperty("is_error", out var ie) && ie.GetBoolean();

        if (root.TryGetProperty("total_cost_usd", out var cost) &&
            cost.ValueKind == JsonValueKind.Number)
            stats.CostUsd = cost.GetDecimal();

        if (root.TryGetProperty("usage", out var usage))
            ApplyUsage(usage, stats);
    }

    private static void ApplyUsage(JsonElement usage, SessionStats stats)
    {
        if (usage.TryGetProperty("input_tokens", out var it) && it.ValueKind == JsonValueKind.Number)
            stats.InputTokens = it.GetInt32();
        if (usage.TryGetProperty("cache_read_input_tokens", out var cr) && cr.ValueKind == JsonValueKind.Number)
            stats.CacheReadTokens = cr.GetInt32();
        if (usage.TryGetProperty("cache_creation_input_tokens", out var cc) && cc.ValueKind == JsonValueKind.Number)
            stats.CacheCreationTokens = cc.GetInt32();
        if (usage.TryGetProperty("output_tokens", out var ot) && ot.ValueKind == JsonValueKind.Number)
            stats.OutputTokens = ot.GetInt32();
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken ct)
    {
        try { await reader.ReadToEndAsync(ct); } catch { }
    }

    public static string StripAnsi(string text) => AnsiEscape.Replace(text, "");
}
