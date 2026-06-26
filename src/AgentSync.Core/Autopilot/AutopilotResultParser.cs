using System.Text.Json;

namespace AgentSync.Core.Autopilot;

/// <summary>
/// Parses Claude CLI output into an <see cref="AutopilotResult"/>.
/// Extracted from <see cref="ClaudeAutopilotProvider"/> so it can be unit-tested without
/// spawning a process.
/// </summary>
public static class AutopilotResultParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Parses the NDJSON envelope emitted by <c>claude --output-format json --json-schema</c>.
    /// Reads <c>structured_output</c> from the <c>result</c> line; falls back to parsing
    /// <c>result</c> as a raw JSON string if the schema did not engage.
    /// </summary>
    public static AutopilotResult ParseFromEnvelope(string ndjsonOutput)
    {
        var text = ndjsonOutput.Trim();
        if (string.IsNullOrEmpty(text))
            return AutopilotResult.ParseError("Empty output from parse step.");

        // With --output-format json (no --verbose), output is typically one line.
        // Find the last non-empty line that looks like a JSON object.
        var lastLine = text
            .Split('\n')
            .Select(l => l.Trim())
            .LastOrDefault(l => l.StartsWith('{'));

        if (lastLine is null)
            return AutopilotResult.ParseError("No JSON object found in parse step output.");

        try
        {
            using var doc = JsonDocument.Parse(lastLine);
            var root = doc.RootElement;

            // Parse step itself errored (e.g. rate limit, trust dialog, crash).
            if (root.TryGetProperty("is_error", out var isErrProp) && isErrProp.GetBoolean())
                return new AutopilotResult(
                    Failed: true, Done: false,
                    Message: "Parse step failed (rate limit or execution error); will retry.",
                    Retry: new AutopilotRetry(300));

            // Ideal path: schema engaged, structured_output is present.
            if (root.TryGetProperty("structured_output", out var structProp))
                return DeserializeDto(structProp.GetRawText());

            // Schema did not engage (stop_reason: end_turn, model answered in prose).
            // Fall back to parsing the result string as raw JSON.
            if (root.TryGetProperty("result", out var resultProp) &&
                resultProp.GetString() is { Length: > 0 } resultStr)
                return Parse(resultStr);

            return AutopilotResult.ParseError(
                "structured_output absent and no result string. " +
                "Check that the prompt matches the schema and stop_reason is 'tool_use'.");
        }
        catch (JsonException ex)
        {
            return AutopilotResult.ParseError($"Envelope parse failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a raw JSON string (possibly wrapped in markdown fences) into an
    /// <see cref="AutopilotResult"/>. Used as a fallback when the schema did not engage
    /// and as a test seam. Returns a failed/done result on any parse error.
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
                text = text[(newline + 1)..closing].Trim();
        }

        return DeserializeDto(text);
    }

    // -------------------------------------------------------------------------

    private static AutopilotResult DeserializeDto(string json)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<AutopilotResultDto>(json, Options);
            if (dto is null)
                return AutopilotResult.ParseError("JSON deserialized to null.");

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
