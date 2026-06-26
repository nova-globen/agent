namespace AgentSync.Core.Autopilot;

public sealed record AutopilotSessionStats(
    int InputTokens,
    int CacheReadTokens,
    int CacheCreationTokens,
    int OutputTokens,
    int ToolCallCount,
    int RateLimitHits,
    decimal CostUsd,
    TimeSpan Elapsed);
