using AgentSync.Core;
using AgentSync.Core.Authoring;
using AgentSync.Core.Import;
using AgentSync.Ui.Abstractions;

namespace AgentSync.Ui.Abstractions.Tests;

/// <summary>
/// Covers the GUI MVP capabilities at the layer the UI actually uses: every screen
/// action goes through <see cref="AgentSyncApp"/>. The localhost web UI
/// (<c>AgentSync.Ui.Web</c>) renders these; these tests prove the capabilities the
/// screens depend on, with no renderer.
/// </summary>
public sealed class MvpCapabilityTests : IDisposable
{
    private readonly string _dir;

    public MvpCapabilityTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "agentsync-ui-mvp", Guid.NewGuid().ToString("N"));
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

    [Fact]
    public void Dashboard_CanOpenRepoAndShowInitState()
    {
        var app = Init();
        var state = app.GetState();
        Assert.True(state.Initialized);
        Assert.Equal(2, state.SkillCount);
    }

    [Fact]
    public void Status_CanShowDrift()
    {
        var app = Init();
        // Before sync, projections are missing => drift.
        Assert.True(app.Drift().HasDrift);

        app.Sync();
        Assert.False(app.Drift().HasDrift);
    }

    [Fact]
    public void Diff_IsAvailable()
    {
        var app = Init();
        var diff = app.Diff();
        // Before sync everything differs (missing projections).
        Assert.True(diff.HasDifferences);
    }

    [Fact]
    public void Validate_IsAvailable()
    {
        var app = Init();
        Assert.True(app.Validate().IsValid);
    }

    [Fact]
    public void Sync_WithConfirmation_Runs()
    {
        var app = Init();
        var report = app.Sync(force: false, dryRun: false);
        Assert.True(report.ConfigValid);
        Assert.True(report.AnyChanges);
    }

    [Fact]
    public void Skills_CanAddEditDelete()
    {
        var app = Init();

        Assert.Equal(AuthoringStatus.Ok, app.AddSkill("docs", "Docs", "Docs review.", null, null).Status);
        Assert.Equal(AuthoringStatus.Ok, app.EditSkill("docs", null, "Updated.", null, null, null, null).Status);
        Assert.Contains(app.ListSkills(), s => s.Id == "docs" && s.Manifest.Description == "Updated.");

        // Not yet synced => safe to delete without force.
        Assert.Equal(AuthoringStatus.Ok, app.DeleteSkill("docs", force: false, dryRun: false).Status);
        Assert.DoesNotContain(app.ListSkills(), s => s.Id == "docs");
    }

    [Fact]
    public void Skills_DeleteBlockedAfterSync_UnlessForce()
    {
        var app = Init();
        app.Sync();

        Assert.Equal(AuthoringStatus.Blocked, app.DeleteSkill("code-review", force: false, dryRun: false).Status);
        Assert.Equal(AuthoringStatus.Ok, app.DeleteSkill("code-review", force: true, dryRun: false).Status);
    }

    [Fact]
    public void Targets_CanAddEditDelete()
    {
        var app = Init();

        // gemini is configured by init; edit then delete it.
        Assert.Equal(AuthoringStatus.Ok, app.EditTarget("gemini", null, enabled: false).Status);
        Assert.Equal(AuthoringStatus.Ok, app.DeleteTarget("gemini", force: false, dryRun: false).Status);
        Assert.False(app.GetConfig()!.Targets.ContainsKey("gemini"));

        // Re-add it.
        Assert.Equal(AuthoringStatus.Ok, app.AddTarget("gemini", ".gemini/GEMINI.md", enabled: true).Status);
        Assert.True(app.GetConfig()!.Targets.ContainsKey("gemini"));
    }

    [Fact]
    public void Imports_CanImportSkillThroughSharedServices()
    {
        var app = Init();
        var srcDir = Path.Combine(_dir, "incoming", "audit");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "SKILL.md"), "---\nname: Audit\ndescription: Audit.\n---\n\n## Use\n\nx\n");

        var report = app.ImportSkill("incoming/audit", new SkillImportOptions());

        Assert.Equal(ImportStatus.Ok, report.Status);
        Assert.Contains(app.ListSkills(), s => s.Id == "audit");
    }

    [Fact]
    public void Imports_CanImportAgentThroughSharedServices()
    {
        var app = Init();
        var legacy = Path.Combine(_dir, "legacy");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "AGENTS.md"), "# Guide\n\nHouse style.\n");

        var report = app.ImportAgent("legacy/AGENTS.md", new AgentImportOptions());

        Assert.Equal(ImportStatus.Ok, report.Status);
    }

    [Fact]
    public void HooksCi_ExposesCopyableCommand()
    {
        Assert.Equal("agent status --fail-on-drift --ci", AgentSyncApp.CiCommand);
    }
}
