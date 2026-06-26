using AgentSync.Core.Autopilot;

namespace AgentSync.Core.Tests.Autopilot;

public sealed class AutopilotResultTests
{
    [Fact]
    public void ParseError_ReturnsFailed_Done_WithMessage()
    {
        var result = AutopilotResult.ParseError("bad json");

        Assert.True(result.Failed);
        Assert.True(result.Done);
        Assert.Contains("bad json", result.Message);
        Assert.Null(result.Retry);
    }

    // ---- AutopilotResultParser.ParseFromEnvelope ----------------------------

    [Fact]
    public void ParseFromEnvelope_StructuredOutput_ReturnsResult()
    {
        var envelope = """{"type":"result","subtype":"success","is_error":false,"stop_reason":"tool_use","structured_output":{"failed":false,"done":true,"message":"All done.","retry":{"afterSeconds":0}}}""";

        var result = AutopilotResultParser.ParseFromEnvelope(envelope);

        Assert.False(result.Failed);
        Assert.True(result.Done);
        Assert.Equal("All done.", result.Message);
        Assert.Null(result.Retry);
    }

    [Fact]
    public void ParseFromEnvelope_StructuredOutput_WithRetry()
    {
        var envelope = """{"type":"result","subtype":"success","is_error":false,"stop_reason":"tool_use","structured_output":{"failed":true,"done":false,"message":"Rate limit hit.","retry":{"afterSeconds":300}}}""";

        var result = AutopilotResultParser.ParseFromEnvelope(envelope);

        Assert.True(result.Failed);
        Assert.False(result.Done);
        Assert.NotNull(result.Retry);
        Assert.Equal(300, result.Retry!.AfterSeconds);
    }

    [Fact]
    public void ParseFromEnvelope_IsError_ReturnsTransientFailure()
    {
        var envelope = """{"type":"result","subtype":"error_during_execution","is_error":true,"stop_reason":null}""";

        var result = AutopilotResultParser.ParseFromEnvelope(envelope);

        Assert.True(result.Failed);
        Assert.False(result.Done);
        Assert.NotNull(result.Retry);
    }

    [Fact]
    public void ParseFromEnvelope_SchemaDidNotEngage_FallsBackToResultString()
    {
        // stop_reason: end_turn means the schema didn't engage; result contains raw JSON.
        var innerJson = """{"failed":false,"done":true,"message":"Fallback worked.","retry":{"afterSeconds":0}}""";
        var envelope = $$"""{"type":"result","subtype":"success","is_error":false,"stop_reason":"end_turn","result":"{{innerJson.Replace("\"", "\\\"")}}"}""";

        var result = AutopilotResultParser.ParseFromEnvelope(envelope);

        Assert.False(result.Failed);
        Assert.True(result.Done);
        Assert.Equal("Fallback worked.", result.Message);
    }

    [Fact]
    public void ParseFromEnvelope_Empty_ReturnsParseError()
    {
        var result = AutopilotResultParser.ParseFromEnvelope(string.Empty);

        Assert.True(result.Failed);
        Assert.True(result.Done);
        Assert.Contains("Empty", result.Message);
    }

    [Fact]
    public void ParseFromEnvelope_NoJson_ReturnsParseError()
    {
        var result = AutopilotResultParser.ParseFromEnvelope("not json at all");

        Assert.True(result.Failed);
        Assert.True(result.Done);
    }

    [Fact]
    public void ParseFromEnvelope_IgnoresLeadingStderrLines()
    {
        // stderr warnings may appear before the JSON line in mixed output
        var envelope =
            "Warning: workspace not trusted.\n" +
            """{"type":"result","subtype":"success","is_error":false,"stop_reason":"tool_use","structured_output":{"failed":false,"done":false,"message":"Partial.","retry":{"afterSeconds":0}}}""";

        var result = AutopilotResultParser.ParseFromEnvelope(envelope);

        Assert.False(result.Failed);
        Assert.False(result.Done);
        Assert.Equal("Partial.", result.Message);
    }

    // ---- AutopilotResultParser.Parse (raw JSON / markdown fallback) ---------

    [Fact]
    public void Parse_PlainJson_ReturnsResult()
    {
        var json = """{"failed":false,"done":true,"message":"Done.","retry":{"afterSeconds":0}}""";

        var result = AutopilotResultParser.Parse(json);

        Assert.False(result.Failed);
        Assert.True(result.Done);
        Assert.Equal("Done.", result.Message);
        Assert.Null(result.Retry);
    }

    [Fact]
    public void Parse_MarkdownFencedJson_ReturnsResult()
    {
        var json = "```json\n{\"failed\":false,\"done\":true,\"message\":\"Done.\",\"retry\":{\"afterSeconds\":0}}\n```";

        var result = AutopilotResultParser.Parse(json);

        Assert.False(result.Failed);
        Assert.True(result.Done);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsParseError()
    {
        var result = AutopilotResultParser.Parse("this is not json");

        Assert.True(result.Failed);
        Assert.True(result.Done);
        Assert.Contains("Parse error", result.Message);
    }
}
