using System.Diagnostics;
using System.Text.Json;

namespace AutopilotTui;

public sealed record Verdict(bool Failed, bool Done, string Message, int RetryAfterSeconds);

public static class StructuredVerdict
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    private const string Schema =
        """{"type":"object","additionalProperties":false,"required":["failed","done","message","retry"],"properties":{"failed":{"type":"boolean"},"done":{"type":"boolean"},"message":{"type":"string","description":"One-paragraph summary. Non-empty."},"retry":{"type":"object","additionalProperties":false,"required":["afterSeconds"],"properties":{"afterSeconds":{"type":"integer","description":"Seconds to wait. 0 = no retry."}}}}}""";

    private const string Prompt =
        "A headless autopilot session just completed in this repository. " +
        "Determine the outcome:\n" +
        "1. Run: git log --oneline -5\n" +
        "2. List .agent/prompts/autopilot/ and read the newest prompt-*.txt file (if any).\n" +
        "Report the verdict: whether it failed, whether done, a summary, and retry seconds (0 = no retry).\n" +
        "Rules:\n" +
        "- failed=true if error, hard blocker, or usage-limit hit.\n" +
        "- done=true if all work complete OR unrecoverable blocker.\n" +
        "- retry.afterSeconds > 0 only for transient failures.\n" +
        "- If a new handoff prompt exists: done=false, failed=false.";

    public static async Task<Verdict> GetAsync(string repoPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("claude")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = repoPath,
        };
        psi.ArgumentList.Add("--dangerously-skip-permissions");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("json");
        psi.ArgumentList.Add("--json-schema");
        psi.ArgumentList.Add(Schema);
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(Prompt);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start 'claude'.");

        process.StandardInput.Close();

        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        var raw = await process.StandardOutput.ReadToEndAsync(ct);
        await stderrTask;
        await process.WaitForExitAsync(ct);

        return Parse(raw.Trim());
    }

    private static Verdict Parse(string ndjson)
    {
        var lastLine = ndjson
            .Split('\n')
            .Select(l => l.Trim())
            .LastOrDefault(l => l.StartsWith('{'));

        if (lastLine is null)
            return new Verdict(true, false, "Parse step returned no JSON.", 300);

        try
        {
            using var doc = JsonDocument.Parse(lastLine);
            var root = doc.RootElement;

            if (root.TryGetProperty("is_error", out var ie) && ie.GetBoolean())
                return new Verdict(true, false, "Parse step failed (rate limit or error).", 300);

            if (root.TryGetProperty("structured_output", out var so))
            {
                var dto = JsonSerializer.Deserialize<VerdictDto>(so.GetRawText(), Opts);
                if (dto is not null)
                    return new Verdict(dto.Failed, dto.Done, dto.Message ?? "", dto.Retry?.AfterSeconds ?? 0);
            }

            return new Verdict(true, false, "structured_output absent from parse step.", 60);
        }
        catch (JsonException ex)
        {
            return new Verdict(true, false, $"JSON parse error: {ex.Message}", 60);
        }
    }

    private sealed class VerdictDto
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
