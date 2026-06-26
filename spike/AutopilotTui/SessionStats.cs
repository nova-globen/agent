namespace AutopilotTui;

public sealed class SessionStats
{
    public int SessionNumber { get; set; }
    public int InputTokens { get; set; }
    public int CacheReadTokens { get; set; }
    public int CacheCreationTokens { get; set; }
    public int OutputTokens { get; set; }
    public int ToolCallCount { get; set; }
    public int RateLimitHits { get; set; }
    public decimal CostUsd { get; set; }
    public TimeSpan Elapsed { get; set; }
    public bool IsRunning { get; set; }
    public bool IsError { get; set; }

    public int TotalInputTokens => InputTokens + CacheReadTokens + CacheCreationTokens;
}
