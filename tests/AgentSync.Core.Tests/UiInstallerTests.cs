using AgentSync.Core;

namespace AgentSync.Core.Tests;

public sealed class UiInstallerTests
{
    [Theory]
    [InlineData("linux-x64", "tar.gz")]
    [InlineData("linux-arm64", "tar.gz")]
    [InlineData("osx-arm64", "tar.gz")]
    [InlineData("win-x64", "zip")]
    public void DownloadUrl_MatchesReleaseArtifactNaming(string rid, string ext)
    {
        var url = UiInstaller.DownloadUrl("0.2.0-alpha.1", rid);

        Assert.Equal(
            $"https://github.com/nova-globen/agent/releases/download/v0.2.0-alpha.1/agent-sync-ui-v0.2.0-alpha.1-{rid}.{ext}",
            url);
    }

    [Fact]
    public void ResolveRid_ReturnsAPublishedRuntimeIdentifier()
    {
        var published = new[] { "linux-x64", "linux-arm64", "osx-x64", "osx-arm64", "win-x64" };

        Assert.Contains(UiInstaller.ResolveRid(), published);
    }

    [Fact]
    public void CacheDirectory_IsVersionAndRidScopedUnderUserProfile()
    {
        var dir = UiInstaller.CacheDirectory("0.2.0-alpha.1", "linux-x64");

        Assert.Contains(Path.Combine(".agent-sync", "ui", "v0.2.0-alpha.1", "linux-x64"), dir);
    }

    [Fact]
    public void Install_UnreleasedVersion_ReturnsNullWithoutSideEffects()
    {
        var installer = new UiInstaller(new NullLauncher());
        using var log = new StringWriter();
        using var err = new StringWriter();

        Assert.Null(installer.Install("0.0.0", log, err));
        Assert.Null(installer.Install("", log, err));
    }

    private sealed class NullLauncher : IUiLauncher
    {
        public string? Locate() => null;
        public bool Launch(UiLaunchRequest request) => false;
    }
}
