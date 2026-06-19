using AgentSync.Ui.Web;

namespace AgentSync.Ui.Web.Tests;

public sealed class WebHostLogicTests
{
    [Fact]
    public void WebOptions_ParsesRepoPortToken()
    {
        var options = WebOptions.Parse(new[] { "--repo", "/work/repo", "--port", "5123", "--token", "abc123" });

        Assert.Equal("/work/repo", options.Repo);
        Assert.Equal(5123, options.Port);
        Assert.Equal("abc123", options.Token);
        Assert.False(options.NoOpen);
    }

    [Fact]
    public void WebOptions_NoOpen_IsParsed()
    {
        var options = WebOptions.Parse(new[] { "--repo", "/r", "--port", "1", "--token", "t", "--no-open" });

        Assert.True(options.NoOpen);
    }

    [Fact]
    public void WebOptions_DefaultsRepoToCwd_WhenMissing()
    {
        var options = WebOptions.Parse(Array.Empty<string>());

        Assert.Equal(Directory.GetCurrentDirectory(), options.Repo);
        Assert.Equal(string.Empty, options.Token);
    }

    [Fact]
    public void TokenCheck_MatchesIdenticalTokens()
    {
        Assert.True(TokenCheck.Matches("s3cret-token", "s3cret-token"));
    }

    [Theory]
    [InlineData("expected", "different")]
    [InlineData("expected", "")]
    [InlineData("expected", null)]
    [InlineData("", "anything")]
    public void TokenCheck_RejectsMismatchOrEmpty(string expected, string? provided)
    {
        Assert.False(TokenCheck.Matches(expected, provided));
    }

    [Fact]
    public void Web_DoesNotReferenceMaui()
    {
        foreach (var referenced in typeof(WebOptions).Assembly.GetReferencedAssemblies())
        {
            var name = referenced.Name ?? string.Empty;
            Assert.DoesNotContain("maui", name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
