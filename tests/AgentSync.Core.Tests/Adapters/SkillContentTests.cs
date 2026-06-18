using AgentSync.Core.Adapters;

namespace AgentSync.Core.Tests.Adapters;

public sealed class SkillContentTests
{
    [Fact]
    public void StripRedundantHeading_RemovesLeadingHeadingMatchingName()
    {
        var body = "# Code Review\n\n## When to use\n\nReview PRs.\n";

        var result = SkillContent.StripRedundantHeading(body, "Code Review");

        Assert.StartsWith("## When to use", result);
        Assert.DoesNotContain("# Code Review", result);
    }

    [Fact]
    public void StripRedundantHeading_IsCaseInsensitive()
    {
        var body = "# code review\n\nBody.\n";

        var result = SkillContent.StripRedundantHeading(body, "Code Review");

        Assert.Equal("Body.", result);
    }

    [Fact]
    public void StripRedundantHeading_KeepsHeadingThatDoesNotMatch()
    {
        var body = "# Overview\n\nBody.\n";

        var result = SkillContent.StripRedundantHeading(body, "Code Review");

        Assert.StartsWith("# Overview", result);
    }

    [Fact]
    public void StripRedundantHeading_KeepsBodyWithoutLeadingHeading()
    {
        var body = "## When to use\n\nBody.\n";

        var result = SkillContent.StripRedundantHeading(body, "Code Review");

        Assert.Equal("## When to use\n\nBody.", result);
    }

    [Fact]
    public void StripRedundantHeading_DoesNotTreatH2AsRedundant()
    {
        var body = "## Code Review\n\nBody.\n";

        var result = SkillContent.StripRedundantHeading(body, "Code Review");

        Assert.StartsWith("## Code Review", result);
    }
}
