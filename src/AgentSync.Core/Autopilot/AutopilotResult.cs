namespace AgentSync.Core.Autopilot;

/// <summary>
/// Structured verdict extracted from a headless AI-agent session output.
/// </summary>
/// <param name="Failed">
/// <c>true</c> when the session ended with an error, network failure, or unrecoverable blocker.
/// </param>
/// <param name="Done">
/// <c>true</c> when the loop should stop — either all work is complete (<see cref="Failed"/>
/// is <c>false</c>) or a hard blocker was hit (<see cref="Failed"/> is <c>true</c>).
/// </param>
/// <param name="Message">One-paragraph markdown summary of what happened.</param>
/// <param name="Retry">
/// When set, the loop should wait <see cref="AutopilotRetry.AfterSeconds"/> before retrying.
/// </param>
public sealed record AutopilotResult(
    bool Failed,
    bool Done,
    string Message,
    AutopilotRetry? Retry)
{
    public static AutopilotResult ParseError(string detail)
        => new(Failed: true, Done: true, Message: $"Parse error: {detail}", Retry: null);
}

public sealed record AutopilotRetry(int AfterSeconds);
