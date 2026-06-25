using AgentSync.Core.Autopilot;

namespace AgentSync.Core.Tests.Autopilot;

public sealed class AutopilotResultParserTests
{
    [Fact]
    public void Parse_PlainJson_DoneSuccess_Parsed()
    {
        var json = """{"failed":false,"done":true,"message":"All done."}""";

        var result = AutopilotResultParser.Parse(json);

        Assert.False(result.Failed);
        Assert.True(result.Done);
        Assert.Equal("All done.", result.Message);
        Assert.Null(result.Retry);
    }

    [Fact]
    public void Parse_PlainJson_WithRetry_Parsed()
    {
        var json = """{"failed":true,"done":false,"message":"Usage limit hit.","retry":{"afterSeconds":3600}}""";

        var result = AutopilotResultParser.Parse(json);

        Assert.True(result.Failed);
        Assert.False(result.Done);
        Assert.Equal("Usage limit hit.", result.Message);
        Assert.NotNull(result.Retry);
        Assert.Equal(3600, result.Retry!.AfterSeconds);
    }

    [Fact]
    public void Parse_JsonInMarkdownFences_StrippedAndParsed()
    {
        var json = """
            ```json
            {"failed":false,"done":true,"message":"Complete."}
            ```
            """;

        var result = AutopilotResultParser.Parse(json);

        Assert.False(result.Failed);
        Assert.True(result.Done);
        Assert.Equal("Complete.", result.Message);
    }

    [Fact]
    public void Parse_JsonInUnlabelledFences_StrippedAndParsed()
    {
        var json = "```\n{\"failed\":false,\"done\":true,\"message\":\"Done.\"}\n```";

        var result = AutopilotResultParser.Parse(json);

        Assert.False(result.Failed);
        Assert.True(result.Done);
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsParsedError()
    {
        var result = AutopilotResultParser.Parse("this is not json");

        Assert.True(result.Failed);
        Assert.True(result.Done);
        Assert.Contains("Parse error", result.Message);
        Assert.Null(result.Retry);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsParsedError()
    {
        var result = AutopilotResultParser.Parse(string.Empty);

        Assert.True(result.Failed);
        Assert.True(result.Done);
    }

    [Fact]
    public void Parse_RetryWithZeroSeconds_TreatedAsNoRetry()
    {
        // afterSeconds=0 is intentionally ignored — a zero-wait retry is not meaningful.
        var json = """{"failed":true,"done":false,"message":"Hmm.","retry":{"afterSeconds":0}}""";

        var result = AutopilotResultParser.Parse(json);

        Assert.Null(result.Retry);
    }

    [Fact]
    public void Parse_HardBlocker_FailedDoneNoRetry()
    {
        var json = """{"failed":true,"done":true,"message":"Unrecoverable error."}""";

        var result = AutopilotResultParser.Parse(json);

        Assert.True(result.Failed);
        Assert.True(result.Done);
        Assert.Null(result.Retry);
    }

    [Fact]
    public void Parse_PartialWork_NotDoneNotFailed()
    {
        var json = """{"failed":false,"done":false,"message":"One increment done, more to go."}""";

        var result = AutopilotResultParser.Parse(json);

        Assert.False(result.Failed);
        Assert.False(result.Done);
        Assert.Null(result.Retry);
    }

    [Fact]
    public void Parse_ExtraWhitespace_HandledGracefully()
    {
        var json = "   \n" + """{"failed":false,"done":true,"message":"ok"}""" + "\n   ";

        var result = AutopilotResultParser.Parse(json);

        Assert.False(result.Failed);
        Assert.True(result.Done);
    }
}
