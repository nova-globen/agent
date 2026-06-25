namespace AgentSync.Core.Autopilot;

/// <summary>
/// Drives a single headless AI-agent CLI session and extracts a structured verdict from its output.
/// </summary>
public interface IAutopilotProvider
{
    /// <summary>Human-readable provider name (e.g. <c>"claude"</c>).</summary>
    string Name { get; }

    /// <summary>Returns <c>true</c> when the provider's CLI is available on PATH.</summary>
    bool IsAvailable();

    /// <summary>
    /// Runs one headless session, streaming output to <paramref name="consoleOut"/> in real-time.
    /// Returns the full captured output when the session completes.
    /// </summary>
    Task<string> RunSessionAsync(TextWriter consoleOut, CancellationToken ct);

    /// <summary>
    /// Passes <paramref name="sessionOutput"/> to the provider's CLI and asks it to extract a
    /// structured <see cref="AutopilotResult"/> — without any further user interaction.
    /// </summary>
    Task<AutopilotResult> ParseResultAsync(string sessionOutput, CancellationToken ct);
}
