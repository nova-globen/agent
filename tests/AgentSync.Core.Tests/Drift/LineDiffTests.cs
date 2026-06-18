using AgentSync.Core.Drift;

namespace AgentSync.Core.Tests.Drift;

public sealed class LineDiffTests
{
    [Fact]
    public void Compute_IdenticalText_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, LineDiff.Compute("a\nb\nc", "a\nb\nc"));
    }

    [Fact]
    public void Compute_IgnoresLineEndingStyle()
    {
        Assert.Equal(string.Empty, LineDiff.Compute("a\nb", "a\r\nb"));
    }

    [Fact]
    public void Compute_ShowsAddedAndRemovedLines()
    {
        var diff = LineDiff.Compute("one\ntwo\nthree", "one\nTWO\nthree");

        Assert.Contains("- two", diff);
        Assert.Contains("+ TWO", diff);
        Assert.Contains("  one", diff);
        Assert.Contains("  three", diff);
    }

    [Fact]
    public void Compute_FromEmpty_AllAdded()
    {
        var diff = LineDiff.Compute(string.Empty, "x\ny");

        Assert.Contains("+ x", diff);
        Assert.Contains("+ y", diff);
        Assert.DoesNotContain("- ", diff);
    }
}
