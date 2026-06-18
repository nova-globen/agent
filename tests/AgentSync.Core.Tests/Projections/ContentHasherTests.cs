using AgentSync.Core.Projections;

namespace AgentSync.Core.Tests.Projections;

public sealed class ContentHasherTests
{
    [Fact]
    public void Hash_HasSha256Prefix()
    {
        Assert.StartsWith("sha256:", ContentHasher.Hash("hello"));
    }

    [Fact]
    public void Hash_IsInsensitiveToLineEndings()
    {
        Assert.Equal(ContentHasher.Hash("a\nb\nc"), ContentHasher.Hash("a\r\nb\r\nc"));
    }

    [Fact]
    public void Hash_IsInsensitiveToTrailingWhitespaceAndBlankLines()
    {
        Assert.Equal(ContentHasher.Hash("line one\nline two"), ContentHasher.Hash("line one   \nline two\n\n"));
    }

    [Fact]
    public void Hash_DiffersForDifferentContent()
    {
        Assert.NotEqual(ContentHasher.Hash("alpha"), ContentHasher.Hash("beta"));
    }

    [Fact]
    public void Matches_ReturnsTrueForSameNormalizedContent()
    {
        var hash = ContentHasher.Hash("body text");
        Assert.True(ContentHasher.Matches("body text\n", hash));
        Assert.False(ContentHasher.Matches("body text edited", hash));
    }
}
