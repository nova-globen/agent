namespace AgentSync.Core.Autopilot;

/// <summary>
/// Drives the autopilot outer loop: invokes the provider repeatedly, handling retries,
/// inter-session delays, and termination conditions.
/// </summary>
public sealed class AutopilotService
{
    /// <summary>
    /// Runs the autopilot loop until all work is done, a hard blocker is hit, or
    /// <paramref name="ct"/> is cancelled. Headless/CI path (no TUI observer).
    /// </summary>
    public Task<int> RunAsync(
        IAutopilotProvider provider,
        AutopilotOptions options,
        TextWriter consoleOut,
        TextWriter consoleErr,
        CancellationToken ct)
        => RunAsync(provider, options, consoleOut, consoleErr, observer: null, ct);

    /// <summary>
    /// Runs the autopilot loop until all work is done, a hard blocker is hit, or
    /// <paramref name="ct"/> is cancelled.
    /// When <paramref name="observer"/> is non-null the TUI receives live events and
    /// console noise is suppressed; when null the loop writes plain-text status lines.
    /// </summary>
    /// <returns>
    /// <see cref="ExitCodes.Success"/> when all work completes cleanly;
    /// <see cref="ExitCodes.DriftOrValidationFailed"/> on hard failure.
    /// </returns>
    public async Task<int> RunAsync(
        IAutopilotProvider provider,
        AutopilotOptions options,
        TextWriter consoleOut,
        TextWriter consoleErr,
        IAutopilotSessionObserver? observer,
        CancellationToken ct)
    {
        if (!provider.IsAvailable())
        {
            consoleErr.WriteLine($"error: '{provider.Name}' CLI not found on PATH.");
            return ExitCodes.EnvironmentProblem;
        }

        var iteration = 0;

        while (!ct.IsCancellationRequested)
        {
            iteration++;

            if (observer is null)
            {
                consoleOut.WriteLine();
                consoleOut.WriteLine($"[autopilot] === session {iteration} starting ===");
                consoleOut.WriteLine();
            }

            observer?.OnSessionStarted(iteration);

            try
            {
                await provider.RunSessionAsync(observer, resumeSessionId: null, ct);
            }
            catch (OperationCanceledException)
            {
                if (observer is null) { consoleOut.WriteLine(); consoleOut.WriteLine("[autopilot] cancelled."); }
                return ExitCodes.Success;
            }
            catch (Exception ex)
            {
                consoleErr.WriteLine($"[autopilot] session {iteration} failed to launch: {ex.Message}");
                return ExitCodes.UnexpectedError;
            }

            if (observer is null) { consoleOut.WriteLine(); consoleOut.WriteLine("[autopilot] session complete. parsing result ..."); }

            AutopilotResult result;
            try
            {
                result = await provider.ParseResultAsync(string.Empty, ct);
            }
            catch (OperationCanceledException)
            {
                if (observer is null) consoleOut.WriteLine("[autopilot] cancelled during parse.");
                return ExitCodes.Success;
            }
            catch (Exception ex)
            {
                consoleErr.WriteLine($"[autopilot] parse step failed: {ex.Message}");
                return ExitCodes.UnexpectedError;
            }

            if (observer is null) consoleOut.WriteLine($"[autopilot] {result.Message}");

            if (result.Retry is not null)
            {
                var wait = result.Retry.AfterSeconds;
                if (observer is null) consoleOut.WriteLine($"[autopilot] retrying in {wait}s ...");
                observer?.OnSessionCompleted(result, wait);
                try { await Task.Delay(TimeSpan.FromSeconds(wait), ct); }
                catch (OperationCanceledException) { return ExitCodes.Success; }
                continue;
            }

            if (result.Done)
            {
                if (observer is null)
                    consoleOut.WriteLine(result.Failed
                        ? "[autopilot] stopped: hard blocker encountered."
                        : "[autopilot] all work complete.");
                observer?.OnSessionCompleted(result, 0);
                return result.Failed ? ExitCodes.DriftOrValidationFailed : ExitCodes.Success;
            }

            // Session completed one chunk of work; pause before next session.
            if (observer is null) consoleOut.WriteLine($"[autopilot] starting next session in {options.DelaySeconds}s ...");
            observer?.OnSessionCompleted(result, options.DelaySeconds);
            try { await Task.Delay(TimeSpan.FromSeconds(options.DelaySeconds), ct); }
            catch (OperationCanceledException) { return ExitCodes.Success; }
        }

        return ExitCodes.Success;
    }
}
