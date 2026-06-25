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
}
