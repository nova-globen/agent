using AgentSync.Core;
using AgentSync.Core.Authoring;
using AgentSync.Core.Configuration;
using AgentSync.Core.Import;
using AgentSync.Ui.Abstractions;

namespace AgentSync.Ui.Abstractions.Tests;

/// <summary>
/// Covers the application-service methods the UI-2 mutation screens call. Each test exercises
/// exactly what a confirmed Razor action invokes on <see cref="AgentSyncApp"/>, so the screens
/// can be thin. No renderer or browser is involved.
/// </summary>
public sealed class Ui2WiringTests : IDisposable
{
    private readonly string _dir;

    public Ui2WiringTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "agentsync-ui2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        Directory.CreateDirectory(Path.Combine(_dir, ".git"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private AgentSyncApp Init()
    {
        new InitService(_dir).Run();
        return new AgentSyncApp(_dir);
    }

    // --- Targets view ---------------------------------------------------------

    [Fact]
    public void ListTargets_ReturnsEveryKnownTarget_InOrder()
    {
        var app = Init();

        var targets = app.ListTargets();

        Assert.Equal(TargetIds.Ordered, targets.Select(t => t.Id).ToList());
        // init configures targets, so gemini should be present + enabled.
        var gemini = targets.Single(t => t.Id == "gemini");
        Assert.True(gemini.Configured);
        Assert.True(gemini.Enabled);
        Assert.False(string.IsNullOrWhiteSpace(gemini.Path));
    }

    [Fact]
    public void ListTargets_ReflectsEditAndDelete()
    {
        var app = Init();

        app.EditTarget("gemini", null, enabled: false);
        Assert.False(app.ListTargets().Single(t => t.Id == "gemini").Enabled);

        app.DeleteTarget("gemini", force: false, dryRun: false);
        Assert.False(app.ListTargets().Single(t => t.Id == "gemini").Configured);
    }

    // --- Skills mutations -----------------------------------------------------

    [Fact]
    public void EditSkill_EnableDisableTargets_RoundTrips()
    {
        var app = Init();
        app.AddSkill("docs", "Docs", "Docs review.", null, new[] { TargetIds.AgentsMd });

        var before = app.GetSkill("docs")!;
        Assert.Contains(TargetIds.AgentsMd, before.EnabledTargets);
        Assert.DoesNotContain(TargetIds.Cursor, before.EnabledTargets);

        var result = app.EditSkill("docs", null, null, null, null,
            enable: new[] { TargetIds.Cursor }, disable: new[] { TargetIds.AgentsMd });

        Assert.Equal(AuthoringStatus.Ok, result.Status);
        var after = app.GetSkill("docs")!;
        Assert.Contains(TargetIds.Cursor, after.EnabledTargets);
        Assert.DoesNotContain(TargetIds.AgentsMd, after.EnabledTargets);
    }

    [Fact]
    public void DeleteSkill_DryRun_WritesNothing()
    {
        var app = Init();
        app.AddSkill("docs", "Docs", "Docs review.", null, null);

        var result = app.DeleteSkill("docs", force: false, dryRun: true);

        Assert.True(result.DryRun);
        Assert.Contains(app.ListSkills(), s => s.Id == "docs");
    }

    // --- Imports (dry run) ----------------------------------------------------

    [Fact]
    public void ImportSkill_DryRun_PreviewsWithoutWriting()
    {
        var app = Init();
        var srcDir = Path.Combine(_dir, "incoming", "triage");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "SKILL.md"), "---\nname: Triage\ndescription: Triage.\n---\n\n## Use\n\nx\n");

        var report = app.ImportSkill("incoming/triage", new SkillImportOptions(DryRun: true));

        Assert.True(report.DryRun);
        Assert.Equal(ImportStatus.Ok, report.Status);
        Assert.DoesNotContain(app.ListSkills(), s => s.Id == "triage");
    }

    [Fact]
    public void ImportAgent_DryRun_PreviewsWithoutWriting()
    {
        var app = Init();
        var legacy = Path.Combine(_dir, "legacy");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "AGENTS.md"), "# Guide\n\nHouse style.\n");
        var before = app.ListSkills().Count;

        var report = app.ImportAgent("legacy/AGENTS.md", new AgentImportOptions(DryRun: true));

        Assert.True(report.DryRun);
        Assert.Equal(ImportStatus.Ok, report.Status);
        Assert.Equal(before, app.ListSkills().Count);
    }

    // --- Sync (dry run) -------------------------------------------------------

    [Fact]
    public void Sync_DryRun_ReportsChangesWithoutWriting()
    {
        var app = Init();

        var preview = app.Sync(force: false, dryRun: true);

        Assert.True(preview.DryRun);
        Assert.True(preview.AnyChanges);
        // Still drifted afterward because the dry run wrote nothing.
        Assert.True(app.Drift().HasDrift);
    }

    // --- Hooks / version ------------------------------------------------------

    [Fact]
    public void InstallHooks_WritesHookScripts()
    {
        var app = Init();

        var result = app.InstallHooks();

        Assert.NotNull(result);
        Assert.Equal(".githooks", result.HooksPath);
        Assert.Equal(2, result.Hooks.Count);
        Assert.All(result.Hooks, h => Assert.True(h.Present));
    }

    [Fact]
    public void AppVersion_IsReported()
    {
        Assert.False(string.IsNullOrWhiteSpace(AgentSyncApp.AppVersion));
    }
}
