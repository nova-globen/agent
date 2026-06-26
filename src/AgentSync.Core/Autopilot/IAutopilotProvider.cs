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
    /// Runs one headless session and returns the <c>session_id</c> assigned by claude
    /// (captured from the <c>system/init</c> NDJSON line in streaming mode; <c>null</c>
    /// in the headless/CI path where stdout is not redirected).
    /// <para>
    /// When <paramref name="observer"/> is non-null, uses <c>--output-format stream-json</c>
    /// and fires typed events on the observer. When <paramref name="observer"/> is null,
    /// stdout flows directly to the terminal (no capture).
    /// </para>
    /// <para>
    /// Pass a <paramref name="resumeSessionId"/> to append <c>--resume &lt;id&gt;</c> and
    /// continue an existing conversation rather than starting a fresh one.
    /// </para>
    /// </summary>
    Task<string?> RunSessionAsync(
        IAutopilotSessionObserver? observer,
        string? resumeSessionId,
        CancellationToken ct);

    /// <summary>
    /// Passes <paramref name="sessionOutput"/> to the provider's CLI and asks it to extract a
    /// structured <see cref="AutopilotResult"/> — without any further user interaction.
    /// </summary>
    Task<AutopilotResult> ParseResultAsync(string sessionOutput, CancellationToken ct);
}
