namespace AgentSync.Core.Autopilot;

/// <summary>Tuning parameters for the <see cref="AutopilotService"/> loop.</summary>
public sealed record AutopilotOptions(
    /// <summary>Seconds to wait between successful sessions before starting the next one.</summary>
    int DelaySeconds = 5);
