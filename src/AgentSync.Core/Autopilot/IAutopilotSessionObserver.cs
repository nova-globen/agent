namespace AgentSync.Core.Autopilot;

/// <summary>
/// Receives real-time events from a streaming autopilot session.
/// All methods are called from the background thread that drives the session.
/// Implementations must be thread-safe.
/// </summary>
public interface IAutopilotSessionObserver
{
    void OnSessionStarted(int sessionNumber);
    void OnTextDelta(string text);
    void OnToolStarted(string toolName, string toolId);
    void OnToolCompleted(string toolId, bool isError);
    void OnStats(AutopilotSessionStats stats);
    void OnSessionCompleted(AutopilotResult result, int nextDelaySeconds);
}
