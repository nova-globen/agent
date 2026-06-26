using AgentSync.Core.Autopilot;

namespace AgentSync.Core.Tests.Autopilot;

public sealed class ClaudeStreamParserTests
{
    private static (ClaudeStreamParser.Stats, FakeObserver, Dictionary<string, string>) Setup()
        => (new ClaudeStreamParser.Stats(), new FakeObserver(), []);

    // ---- text_delta ----------------------------------------------------------

    [Fact]
    public void DispatchLine_TextDelta_CallsOnTextDelta()
    {
        var (stats, observer, tools) = Setup();
        var line = """{"type":"stream_event","event":{"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"Hello world"}}}""";

        ClaudeStreamParser.DispatchLine(line, stats, observer, tools);

        Assert.Single(observer.Texts);
        Assert.Equal("Hello world", observer.Texts[0]);
    }

    // ---- tool_use start / stop -----------------------------------------------

    [Fact]
    public void DispatchLine_ToolUseStart_CallsOnToolStarted_And_IncrementsCount()
    {
        var (stats, observer, tools) = Setup();
        var line = """{"type":"stream_event","event":{"type":"content_block_start","index":2,"content_block":{"type":"tool_use","id":"tool_001","name":"PowerShell","input":{}}}}""";

        ClaudeStreamParser.DispatchLine(line, stats, observer, tools);

        Assert.Single(observer.ToolsStarted);
        Assert.Equal(("PowerShell", "tool_001"), observer.ToolsStarted[0]);
        Assert.Equal(1, stats.ToolCallCount);
    }

    [Fact]
    public void DispatchLine_TaskNotification_Completed_CallsOnToolCompleted_NotError()
    {
        var (stats, observer, tools) = Setup();
        var line = """{"type":"system","subtype":"task_notification","task_id":"t1","tool_use_id":"tool_001","status":"completed"}""";

        ClaudeStreamParser.DispatchLine(line, stats, observer, tools);

        Assert.Single(observer.ToolsCompleted);
        Assert.Equal(("tool_001", false), observer.ToolsCompleted[0]);
    }

    [Fact]
    public void DispatchLine_TaskNotification_Failed_CallsOnToolCompleted_IsError()
    {
        var (stats, observer, tools) = Setup();
        var line = """{"type":"system","subtype":"task_notification","task_id":"t1","tool_use_id":"tool_002","status":"failed"}""";

        ClaudeStreamParser.DispatchLine(line, stats, observer, tools);

        Assert.Single(observer.ToolsCompleted);
        Assert.Equal(("tool_002", true), observer.ToolsCompleted[0]);
    }

    // ---- rate_limit_event ----------------------------------------------------

    [Fact]
    public void DispatchLine_RateLimitRejected_IncrementsHits()
    {
        var (stats, observer, tools) = Setup();
        var line = """{"type":"rate_limit_event","rate_limit_info":{"status":"rejected","resetsAt":12345}}""";

        ClaudeStreamParser.DispatchLine(line, stats, observer, tools);

        Assert.Equal(1, stats.RateLimitHits);
    }

    [Fact]
    public void DispatchLine_RateLimitAllowed_DoesNotIncrementHits()
    {
        var (stats, observer, tools) = Setup();
        var line = """{"type":"rate_limit_event","rate_limit_info":{"status":"allowed"}}""";

        ClaudeStreamParser.DispatchLine(line, stats, observer, tools);

        Assert.Equal(0, stats.RateLimitHits);
    }

    // ---- result --------------------------------------------------------------

    [Fact]
    public void DispatchLine_Result_UpdatesCostAndTokens()
    {
        var (stats, observer, tools) = Setup();
        var line = """{"type":"result","subtype":"success","is_error":false,"total_cost_usd":0.025,"usage":{"input_tokens":100,"cache_read_input_tokens":500,"cache_creation_input_tokens":50,"output_tokens":200}}""";

        ClaudeStreamParser.DispatchLine(line, stats, observer, tools);

        Assert.Equal(0.025m, stats.CostUsd);
        Assert.Equal(100, stats.InputTokens);
        Assert.Equal(500, stats.CacheReadTokens);
        Assert.Equal(50, stats.CacheCreationTokens);
        Assert.Equal(200, stats.OutputTokens);
        Assert.False(stats.IsError);
    }

    [Fact]
    public void DispatchLine_Result_IsError_SetsIsError()
    {
        var (stats, observer, tools) = Setup();
        var line = """{"type":"result","subtype":"error_during_execution","is_error":true}""";

        ClaudeStreamParser.DispatchLine(line, stats, observer, tools);

        Assert.True(stats.IsError);
    }

    // ---- malformed input -----------------------------------------------------

    [Fact]
    public void DispatchLine_MalformedJson_DoesNotThrow()
    {
        var (stats, observer, tools) = Setup();

        var ex = Record.Exception(() =>
            ClaudeStreamParser.DispatchLine("not valid json {{{", stats, observer, tools));

        Assert.Null(ex);
    }

    [Fact]
    public void DispatchLine_EmptyObject_DoesNotThrow()
    {
        var (stats, observer, tools) = Setup();

        var ex = Record.Exception(() =>
            ClaudeStreamParser.DispatchLine("{}", stats, observer, tools));

        Assert.Null(ex);
    }

    // ---- Snapshot ------------------------------------------------------------

    [Fact]
    public void Snapshot_ReturnsImmutableRecord_WithCorrectValues()
    {
        var stats = new ClaudeStreamParser.Stats
        {
            InputTokens = 10, OutputTokens = 20, CostUsd = 1.5m,
            ToolCallCount = 3, RateLimitHits = 1,
        };

        var snap = stats.Snapshot(TimeSpan.FromSeconds(30));

        Assert.Equal(10, snap.InputTokens);
        Assert.Equal(20, snap.OutputTokens);
        Assert.Equal(1.5m, snap.CostUsd);
        Assert.Equal(3, snap.ToolCallCount);
        Assert.Equal(1, snap.RateLimitHits);
        Assert.Equal(TimeSpan.FromSeconds(30), snap.Elapsed);
    }

    // ---- fake observer -------------------------------------------------------

    private sealed class FakeObserver : IAutopilotSessionObserver
    {
        public List<string> Texts { get; } = [];
        public List<(string Name, string Id)> ToolsStarted { get; } = [];
        public List<(string Id, bool IsError)> ToolsCompleted { get; } = [];
        public List<AutopilotSessionStats> StatSnapshots { get; } = [];

        public void OnSessionStarted(int sessionNumber) { }
        public void OnTextDelta(string text) => Texts.Add(text);
        public void OnToolStarted(string toolName, string toolId) => ToolsStarted.Add((toolName, toolId));
        public void OnToolCompleted(string toolId, bool isError) => ToolsCompleted.Add((toolId, isError));
        public void OnStats(AutopilotSessionStats stats) => StatSnapshots.Add(stats);
        public void OnSessionCompleted(AutopilotResult result, int nextDelaySeconds) { }
    }
}
