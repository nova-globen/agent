using System.Text.Json;

namespace AgentSync.Core.Autopilot;

/// <summary>
/// Parses the raw JSON string emitted by the Claude CLI's parse step into an
/// <see cref="AutopilotResult"/>. Extracted from <see cref="ClaudeAutopilotProvider"/>
/// so it can be unit-tested without spawning a process.
/// </summary>
public static class AutopilotResultParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Strips optional markdown fences and deserializes <paramref name="raw"/> into an
    /// <see cref="AutopilotResult"/>.  Returns a failed/done result on any parse error.
    /// </summary>
    public static AutopilotResult Parse(string raw)
    {
        var text = raw.Trim();

        // Strip markdown code fences if the model wrapped the JSON.
        if (text.StartsWith("```"))
        {
            var newline = text.IndexOf('\n');
            var closing = text.LastIndexOf("```");
            if (newline >= 0 && closing > newline)
            {
                text = text[(newline + 1)..closing].Trim();
            }
        }

        try
        {
            var dto = JsonSerializer.Deserialize<AutopilotResultDto>(text, Options);
            if (dto is null)
            {
                return AutopilotResult.ParseError("JSON deserialized to null.");
            }

            AutopilotRetry? retry = dto.Retry is { AfterSeconds: > 0 }
                ? new AutopilotRetry(dto.Retry.AfterSeconds)
                : null;

            return new AutopilotResult(dto.Failed, dto.Done, dto.Message ?? string.Empty, retry);
        }
        catch (JsonException ex)
        {
            return AutopilotResult.ParseError(ex.Message);
        }
    }

    private sealed class AutopilotResultDto
    {
        public bool Failed { get; set; }
        public bool Done { get; set; }
        public string? Message { get; set; }
        public RetryDto? Retry { get; set; }
    }

    private sealed class RetryDto
    {
        public int AfterSeconds { get; set; }
    }
}
