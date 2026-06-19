using AgentSync.Ui.Web;

namespace AgentSync.Ui.Web.Tests;

public sealed class WebHostLogicTests : IDisposable
{
    private readonly string _repo;

    public WebHostLogicTests()
    {
        _repo = Path.Combine(Path.GetTempPath(), "agentsync-weboptions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_repo);
    }

    public void Dispose()
    {
        try { Directory.Delete(_repo, recursive: true); } catch { /* best effort */ }
    }

    // --- WebOptions.TryParse --------------------------------------------------

    [Fact]
    public void WebOptions_ValidOptions_Parse()
    {
        var ok = WebOptions.TryParse(new[] { "--repo", _repo, "--port", "5123", "--token", "abc123" }, out var options, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(_repo, options!.Repo);
        Assert.Equal(5123, options.Port);
        Assert.Equal("abc123", options.Token);
        Assert.False(options.NoOpen);
    }

    [Fact]
    public void WebOptions_NoOpen_IsParsed()
    {
        var ok = WebOptions.TryParse(new[] { "--repo", _repo, "--port", "5123", "--token", "t", "--no-open" }, out var options, out _);

        Assert.True(ok);
        Assert.True(options!.NoOpen);
    }

    [Fact]
    public void WebOptions_MissingRepo_Fails()
    {
        var ok = WebOptions.TryParse(new[] { "--port", "5123", "--token", "t" }, out var options, out var error);

        Assert.False(ok);
        Assert.Null(options);
        Assert.Contains("--repo", error);
    }

    [Fact]
    public void WebOptions_NonexistentRepo_Fails()
    {
        var ok = WebOptions.TryParse(new[] { "--repo", Path.Combine(_repo, "missing"), "--port", "5123", "--token", "t" }, out var options, out var error);

        Assert.False(ok);
        Assert.Null(options);
        Assert.Contains("does not exist", error);
    }

    [Fact]
    public void WebOptions_MissingPort_Fails()
    {
        var ok = WebOptions.TryParse(new[] { "--repo", _repo, "--token", "t" }, out _, out var error);

        Assert.False(ok);
        Assert.Contains("--port", error);
    }

    [Theory]
    [InlineData("notanint")]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("-1")]
    public void WebOptions_InvalidPort_Fails(string port)
    {
        var ok = WebOptions.TryParse(new[] { "--repo", _repo, "--port", port, "--token", "t" }, out _, out var error);

        Assert.False(ok);
        Assert.Contains("--port", error);
    }

    [Fact]
    public void WebOptions_MissingToken_Fails()
    {
        var ok = WebOptions.TryParse(new[] { "--repo", _repo, "--port", "5123" }, out _, out var error);

        Assert.False(ok);
        Assert.Contains("--token", error);
    }

    [Fact]
    public void WebOptions_EmptyToken_Fails()
    {
        var ok = WebOptions.TryParse(new[] { "--repo", _repo, "--port", "5123", "--token", "" }, out _, out var error);

        Assert.False(ok);
        Assert.Contains("--token", error);
    }

    [Fact]
    public void WebOptions_UnknownOption_Fails()
    {
        var ok = WebOptions.TryParse(new[] { "--repo", _repo, "--port", "5123", "--token", "t", "--bogus" }, out _, out var error);

        Assert.False(ok);
        Assert.Contains("unknown option", error);
    }

    [Fact]
    public void WebOptions_OptionWithoutValue_Fails()
    {
        var ok = WebOptions.TryParse(new[] { "--repo", _repo, "--port", "5123", "--token" }, out _, out var error);

        Assert.False(ok);
        Assert.Contains("requires a value", error);
    }

    // --- TokenCheck -----------------------------------------------------------

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

    // --- SessionGate ----------------------------------------------------------

    [Fact]
    public void SessionGate_ValidCookie_AllowsFromCookie()
    {
        Assert.Equal(SessionDecision.AllowFromCookie, SessionGate.Decide("tok", cookieToken: "tok", queryToken: null));
    }

    [Fact]
    public void SessionGate_ValidQueryOnly_AllowsFromQuery()
    {
        Assert.Equal(SessionDecision.AllowFromQuery, SessionGate.Decide("tok", cookieToken: null, queryToken: "tok"));
    }

    [Fact]
    public void SessionGate_InvalidToken_Denies()
    {
        Assert.Equal(SessionDecision.Deny, SessionGate.Decide("tok", cookieToken: "nope", queryToken: "nope"));
    }

    [Fact]
    public void SessionGate_EmptyConfiguredToken_DeniesEverything()
    {
        Assert.Equal(SessionDecision.Deny, SessionGate.Decide("", cookieToken: "anything", queryToken: "anything"));
    }

    [Fact]
    public void SessionGate_Healthz_IsPublic()
    {
        Assert.True(SessionGate.IsPublicPath("/healthz"));
        Assert.False(SessionGate.IsPublicPath("/"));
        Assert.False(SessionGate.IsPublicPath("/skills"));
    }

    [Fact]
    public void SessionGate_RedirectStripsToken_RootPath()
    {
        var location = SessionGate.RedirectWithoutToken("/", new[]
        {
            new KeyValuePair<string, string?>("token", "abc"),
        });

        Assert.Equal("/", location);
    }

    [Fact]
    public void SessionGate_RedirectStripsToken_PreservesOtherParams()
    {
        var location = SessionGate.RedirectWithoutToken("/skills", new[]
        {
            new KeyValuePair<string, string?>("token", "abc"),
            new KeyValuePair<string, string?>("foo", "bar"),
        });

        Assert.Equal("/skills?foo=bar", location);
        Assert.DoesNotContain("token", location);
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
