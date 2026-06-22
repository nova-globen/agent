using AgentSync.Core.Sessions;

namespace AgentSync.Core.Tests.Sessions;

public sealed class PathRewriterTests
{
    [Fact]
    public void WslToWindows_JsonContent_EscapesBackslashes()
    {
        var r = PathRewriter.Build("/mnt/c/Users/x/proj", "C:\\Users\\x\\new");
        var json = "{\"cwd\":\"/mnt/c/Users/x/proj\",\"file\":\"/mnt/c/Users/x/proj/src/a.cs\"}";

        var result = r.Apply(json, jsonEscaped: true);

        Assert.Equal("{\"cwd\":\"C:\\\\Users\\\\x\\\\new\",\"file\":\"C:\\\\Users\\\\x\\\\new/src/a.cs\"}", result);
        // The rewritten JSON must still parse.
        using var doc = System.Text.Json.JsonDocument.Parse(result);
        Assert.Equal("C:\\Users\\x\\new", doc.RootElement.GetProperty("cwd").GetString());
    }

    [Fact]
    public void WindowsToWsl_PlainText()
    {
        var r = PathRewriter.Build("C:\\Users\\x\\proj", "/mnt/c/Users/x/new");
        var text = "cd C:\\Users\\x\\proj && build";

        Assert.Equal("cd /mnt/c/Users/x/new && build", r.Apply(text, jsonEscaped: false));
    }

    [Fact]
    public void WindowsSource_InJson_MatchesEscapedForm()
    {
        var r = PathRewriter.Build("C:\\Users\\x\\proj", "/home/u/new");
        var json = "{\"cwd\":\"C:\\\\Users\\\\x\\\\proj\"}";

        var result = r.Apply(json, jsonEscaped: true);

        Assert.Equal("{\"cwd\":\"/home/u/new\"}", result);
    }

    [Fact]
    public void SamePathDifferentStyles_AllTranslated()
    {
        var r = PathRewriter.Build("/mnt/c/repo", "/mnt/d/repo");
        // A file mentioning the project in several styles at once.
        var text = "/mnt/c/repo C:/repo and C:\\repo";

        var result = r.Apply(text, jsonEscaped: false);

        Assert.Equal("/mnt/d/repo /mnt/d/repo and /mnt/d/repo", result);
    }

    [Fact]
    public void HomeDirectory_AlsoRewritten()
    {
        var r = PathRewriter.Build(
            "/mnt/c/Users/x/proj", "/mnt/c/Users/y/proj",
            sourceHome: "/home/x", destHome: "/home/y");
        var text = "project /mnt/c/Users/x/proj home /home/x/.claude";

        Assert.Equal("project /mnt/c/Users/y/proj home /home/y/.claude", r.Apply(text, jsonEscaped: false));
    }

    [Fact]
    public void Identity_WhenSourceEqualsDest()
    {
        var r = PathRewriter.Build("/mnt/c/repo", "/mnt/c/repo");
        Assert.True(r.IsIdentity);
        Assert.Equal("unchanged /mnt/c/repo", r.Apply("unchanged /mnt/c/repo", jsonEscaped: false));
    }

    [Fact]
    public void ProjectWinsOverHome_WhenNested()
    {
        // Project path is under home; the longer (project) replacement must apply first so the
        // home replacement does not partially clobber it.
        var r = PathRewriter.Build(
            "/home/x/proj", "/work/new",
            sourceHome: "/home/x", destHome: "/elsewhere");
        var text = "/home/x/proj/file and /home/x/other";

        Assert.Equal("/work/new/file and /elsewhere/other", r.Apply(text, jsonEscaped: false));
    }
}
