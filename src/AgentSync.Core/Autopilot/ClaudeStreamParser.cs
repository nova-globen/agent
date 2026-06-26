using System.Text.Json;

namespace AgentSync.Core.Autopilot;

/// <summary>
/// Parses one NDJSON line from claude's <c>--output-format stream-json</c> output and
/// fires observer callbacks. Extracted for unit-testability without spawning a process.
/// </summary>
public static class ClaudeStreamParser
{
    /// <summary>
    /// Mutable stats bag accumulated across lines in one session.
    /// Create one instance per session; pass to every <see cref="DispatchLine"/> call.
    /// </summary>
    public sealed class Stats
    {
        public int InputTokens;
        public int CacheReadTokens;
        public int CacheCreationTokens;
        public int OutputTokens;
        public int ToolCallCount;
        public int RateLimitHits;
        public decimal CostUsd;
        public bool IsError;

        public AutopilotSessionStats Snapshot(TimeSpan elapsed) => new(
            InputTokens, CacheReadTokens, CacheCreationTokens,
            OutputTokens, ToolCallCount, RateLimitHits, CostUsd, elapsed);
    }

    /// <summary>
    /// Parses one NDJSON line and fires the appropriate observer callbacks.
    /// Swallows <see cref="JsonException"/> so malformed lines are silently skipped.
    /// </summary>
    public static void DispatchLine(
        string line,
        Stats stats,
        IAutopilotSessionObserver? observer,
        Dictionary<string, string> pendingTools)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp)) return;

            switch (typeProp.GetString())
            {
                case "rate_limit_event":
                    if (root.TryGetProperty("rate_limit_info", out var rli) &&
                        rli.TryGetProperty("status", out var st) &&
                        st.GetString() is not "allowed")
                    {
                        stats.RateLimitHits++;
                    }
                    break;

                case "stream_event":
                    HandleStreamEvent(root, stats, observer, pendingTools);
                    break;

                case "system":
                    HandleSystemEvent(root, observer);
                    break;

                case "result":
                    HandleResult(root, stats);
                    break;
            }
        }
        catch (JsonException) { }
    }

    private static void HandleStreamEvent(
        JsonElement root,
        Stats stats,
        IAutopilotSessionObserver? observer,
        Dictionary<string, string> pendingTools)
    {
        if (!root.TryGetProperty("event", out var ev)) return;
        if (!ev.TryGetProperty("type", out var evTypeProp)) return;

        switch (evTypeProp.GetString())
        {
            case "content_block_start":
                if (ev.TryGetProperty("content_block", out var cb) &&
                    cb.TryGetProperty("type", out var cbType) &&
                    cbType.GetString() == "tool_use")
                {
                    var id   = cb.TryGetProperty("id",   out var idp) ? idp.GetString()  ?? "" : "";
                    var name = cb.TryGetProperty("name", out var np)  ? np.GetString()   ?? "tool" : "tool";
                    pendingTools[id] = name;
                    stats.ToolCallCount++;
                    observer?.OnToolStarted(name, id);
                }
                break;

            case "content_block_delta":
                if (!ev.TryGetProperty("delta", out var delta)) break;
                if (!delta.TryGetProperty("type", out var deltaType)) break;
                if (deltaType.GetString() == "text_delta" &&
                    delta.TryGetProperty("text", out var textProp))
                {
                    observer?.OnTextDelta(textProp.GetString() ?? "");
                }
                break;

            case "message_delta":
                if (ev.TryGetProperty("usage", out var usage))
                    ApplyUsage(usage, stats);
                break;
        }
    }

    private static void HandleSystemEvent(JsonElement root, IAutopilotSessionObserver? observer)
    {
        if (!root.TryGetProperty("subtype", out var sub)) return;

        if (sub.GetString() == "task_notification" &&
            root.TryGetProperty("tool_use_id", out var tid) &&
            root.TryGetProperty("status", out var st))
        {
            observer?.OnToolCompleted(tid.GetString() ?? "", st.GetString() != "completed");
        }
    }

    private static void HandleResult(JsonElement root, Stats stats)
    {
        stats.IsError = root.TryGetProperty("is_error", out var ie) && ie.GetBoolean();

        if (root.TryGetProperty("total_cost_usd", out var cost) &&
            cost.ValueKind == JsonValueKind.Number)
            stats.CostUsd = cost.GetDecimal();

        if (root.TryGetProperty("usage", out var usage))
            ApplyUsage(usage, stats);
    }

    private static void ApplyUsage(JsonElement usage, Stats stats)
    {
        if (usage.TryGetProperty("input_tokens",                out var it) && it.ValueKind == JsonValueKind.Number) stats.InputTokens = it.GetInt32();
        if (usage.TryGetProperty("cache_read_input_tokens",     out var cr) && cr.ValueKind == JsonValueKind.Number) stats.CacheReadTokens = cr.GetInt32();
        if (usage.TryGetProperty("cache_creation_input_tokens", out var cc) && cc.ValueKind == JsonValueKind.Number) stats.CacheCreationTokens = cc.GetInt32();
        if (usage.TryGetProperty("output_tokens",               out var ot) && ot.ValueKind == JsonValueKind.Number) stats.OutputTokens = ot.GetInt32();
    }
}
