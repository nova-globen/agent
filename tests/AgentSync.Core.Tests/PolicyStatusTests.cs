namespace AgentSync.Core.Tests;

public sealed class PolicyStatusTests
{
    private static string ConfigPath(string root) => Path.Combine(root, ".agent", "agent.yaml");

    private static void SetPolicy(string root, string flag, bool value)
    {
        var path = ConfigPath(root);
        var text = File.ReadAllText(path);
        text = text.Replace($"{flag}: true", $"{flag}: {value.ToString().ToLowerInvariant()}")
                   .Replace($"{flag}: false", $"{flag}: {value.ToString().ToLowerInvariant()}");
        File.WriteAllText(path, text);
    }

    [Fact]
    public void FailOnMissingProjection_False_DowngradesMissingToWarning()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run(); // no sync → projections missing

        var withDefault = new StatusService(temp.Path).Run();
        Assert.True(withDefault.HasProblems);
        Assert.Contains(withDefault.Issues, i => i.Code == "drift-missing" && i.Severity == IssueSeverity.Error);

        SetPolicy(temp.Path, "fail_on_missing_projection", false);
        var withPolicy = new StatusService(temp.Path).Run();

        Assert.False(withPolicy.HasProblems);
        Assert.Contains(withPolicy.Issues, i => i.Code == "drift-missing" && i.Severity == IssueSeverity.Warning);
    }

    [Fact]
    public void FailOnOutdatedProjection_False_DowngradesOutdatedToWarning()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        new SyncService(temp.Path).Run();
        File.WriteAllText(
            Path.Combine(temp.Path, ".agent", "skills", "code-review", "SKILL.md"),
            "## New section\n\nBrand new canonical content.\n");

        var withDefault = new StatusService(temp.Path).Run();
        Assert.True(withDefault.HasProblems);
        Assert.Contains(withDefault.Issues, i => i.Code == "drift-outdated" && i.Severity == IssueSeverity.Error);

        SetPolicy(temp.Path, "fail_on_outdated_projection", false);
        var withPolicy = new StatusService(temp.Path).Run();

        Assert.False(withPolicy.HasProblems);
        Assert.Contains(withPolicy.Issues, i => i.Code == "drift-outdated" && i.Severity == IssueSeverity.Warning);
    }

    [Fact]
    public void FailOnManualEdit_False_DowngradesManualEditToWarning()
    {
        using var temp = new TempDir();
        new InitService(temp.Path).Run();
        new SyncService(temp.Path).Run();
        var agents = Path.Combine(temp.Path, "AGENTS.md");
        File.WriteAllText(agents, File.ReadAllText(agents).Replace("Reviews changes", "HAND EDIT what"));

        var withDefault = new StatusService(temp.Path).Run();
        Assert.True(withDefault.HasProblems);
        Assert.Contains(withDefault.Issues, i => i.Code == "drift-manual-edit" && i.Severity == IssueSeverity.Error);

        SetPolicy(temp.Path, "fail_on_manual_edit", false);
        var withPolicy = new StatusService(temp.Path).Run();

        Assert.False(withPolicy.HasProblems);
        Assert.Contains(withPolicy.Issues, i => i.Code == "drift-manual-edit" && i.Severity == IssueSeverity.Warning);
    }
}
