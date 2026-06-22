using AgentSync.Core.Sessions;

namespace AgentSync.Core.Tests.Sessions;

public sealed class PathConversionTests
{
    [Theory]
    [InlineData("/mnt/c/Users/x/proj", 'C', "Users/x/proj")]
    [InlineData("C:\\Users\\x\\proj", 'C', "Users/x/proj")]
    [InlineData("C:/Users/x/proj", 'C', "Users/x/proj")]
    [InlineData("d:\\Data", 'D', "Data")]
    public void Parse_DrivePaths(string input, char drive, string segments)
    {
        var loc = LocationPath.Parse(input);
        Assert.NotNull(loc);
        Assert.Equal(drive, loc!.Drive);
        Assert.Equal(segments, string.Join('/', loc.Segments));
    }

    [Fact]
    public void Parse_UnixPath_HasNoDrive()
    {
        var loc = LocationPath.Parse("/home/user/proj");
        Assert.NotNull(loc);
        Assert.Null(loc!.Drive);
        Assert.Equal(new[] { "home", "user", "proj" }, loc.Segments);
    }

    [Fact]
    public void Parse_RelativeOrEmpty_ReturnsNull()
    {
        Assert.Null(LocationPath.Parse("relative/path"));
        Assert.Null(LocationPath.Parse(""));
        Assert.Null(LocationPath.Parse(null));
    }

    [Fact]
    public void Render_TranslatesBetweenWslAndWindows()
    {
        var loc = LocationPath.Parse("/mnt/c/Users/x/proj")!;
        Assert.Equal("C:\\Users\\x\\proj", loc.Render(PathStyle.Windows));
        Assert.Equal("C:/Users/x/proj", loc.Render(PathStyle.WindowsForward));
        Assert.Equal("/mnt/c/Users/x/proj", loc.Render(PathStyle.Wsl));
        Assert.Null(loc.Render(PathStyle.Unix)); // a drive path has no pure-Unix form
    }

    [Fact]
    public void Render_FromWindowsToWsl()
    {
        var loc = LocationPath.Parse("C:\\Users\\x\\proj")!;
        Assert.Equal("/mnt/c/Users/x/proj", loc.Render(PathStyle.Wsl));
    }

    [Fact]
    public void Render_UnixPath_IsStableAndHasNoWindowsForm()
    {
        var loc = LocationPath.Parse("/home/u/p")!;
        Assert.Equal("/home/u/p", loc.Render(PathStyle.Unix));
        Assert.Equal("/home/u/p", loc.Render(PathStyle.Wsl));
        Assert.Null(loc.Render(PathStyle.Windows));
    }

    [Theory]
    [InlineData("/mnt/c/Users/x", PathStyle.Wsl)]
    [InlineData("C:\\Users\\x", PathStyle.Windows)]
    [InlineData("C:/Users/x", PathStyle.WindowsForward)]
    [InlineData("/home/x", PathStyle.Unix)]
    public void DetectStyle(string input, PathStyle expected)
        => Assert.Equal(expected, PathConversion.DetectStyle(input));

    [Fact]
    public void DriveRoots_RoundTrip()
    {
        Assert.Equal("C:\\", LocationPath.Parse("C:\\")!.Render(PathStyle.Windows));
        Assert.Equal("/mnt/c", LocationPath.Parse("/mnt/c")!.Render(PathStyle.Wsl));
    }
}
