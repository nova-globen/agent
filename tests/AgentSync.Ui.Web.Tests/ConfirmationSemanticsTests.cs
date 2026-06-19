using AgentSync.Core;
using AgentSync.Core.Authoring;
using AgentSync.Ui.Abstractions;
using AgentSync.Ui.Web.ViewModels;

namespace AgentSync.Ui.Web.Tests;

/// <summary>
/// Protects the UI's confirmation semantics at the view-model layer (no renderer):
/// add/edit are explicit single-action writes, while destructive/environment-changing
/// operations (delete skill, delete target, force sync, install hooks) only execute after a
/// prior request — i.e. the second confirmation step the Razor renders.
/// </summary>
public sealed class ConfirmationSemanticsTests : IDisposable
{
    private readonly string _dir;

    public ConfirmationSemanticsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "agentsync-confirm", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        Directory.CreateDirectory(Path.Combine(_dir, ".git"));
        new InitService(_dir).Run();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private AgentSyncApp App() => new(_dir);

    // ----- Skills: add/edit explicit; delete gated -----

    [Fact]
    public void Skills_Add_IsExplicit_NotASideEffectOfTyping()
    {
        var vm = new SkillsViewModel(App());
        vm.AddId = "docs";
        vm.AddName = "Docs";
        vm.AddDescription = "Docs review.";

        // Setting the form fields must not have written anything yet.
        Assert.DoesNotContain(vm.Skills, s => s.Id == "docs");

        var result = vm.Add();

        Assert.Equal(AuthoringStatus.Ok, result.Status);
        Assert.Contains(vm.Skills, s => s.Id == "docs");
    }

    [Fact]
    public void Skills_ConfirmDelete_WithoutRequest_DoesNothing()
    {
        var app = App();
        new SkillsViewModel(app).Add(); // no-op safety; ensure app usable
        var vm = new SkillsViewModel(app);
        vm.AddId = "docs";
        vm.AddName = "Docs";
        vm.AddDescription = "Docs review.";
        vm.Add();

        // No delete requested => confirm is a no-op and writes nothing.
        Assert.Null(vm.PendingDeleteId);
        Assert.Null(vm.ConfirmDelete());
        Assert.Contains(vm.Skills, s => s.Id == "docs");

        // After an explicit request, confirm performs the delete.
        vm.RequestDelete("docs");
        Assert.Equal("docs", vm.PendingDeleteId);
        var result = vm.ConfirmDelete();
        Assert.NotNull(result);
        Assert.Equal(AuthoringStatus.Ok, result!.Status);
        Assert.DoesNotContain(vm.Skills, s => s.Id == "docs");
    }

    // ----- Targets: add/edit explicit; delete gated -----

    [Fact]
    public void Targets_ConfirmDelete_WithoutRequest_DoesNothing()
    {
        var vm = new TargetsViewModel(App());

        Assert.Null(vm.PendingDeleteId);
        Assert.Null(vm.ConfirmDelete());
        Assert.True(vm.Targets.Single(t => t.Id == "gemini").Configured);

        vm.RequestDelete("gemini");
        Assert.Equal("gemini", vm.PendingDeleteId);
        var result = vm.ConfirmDelete();
        Assert.NotNull(result);
        Assert.Equal(AuthoringStatus.Ok, result!.Status);
        Assert.False(vm.Targets.Single(t => t.Id == "gemini").Configured);
    }

    [Fact]
    public void Targets_Add_IsExplicit()
    {
        var vm = new TargetsViewModel(App());
        vm.RequestDelete("gemini");
        vm.ConfirmDelete(); // remove so we can re-add

        vm.AddId = "gemini";
        vm.AddPath = ".gemini/GEMINI.md";
        vm.AddEnabled = true;
        Assert.False(vm.Targets.Single(t => t.Id == "gemini").Configured); // not added yet

        var result = vm.Add();
        Assert.Equal(AuthoringStatus.Ok, result.Status);
        Assert.True(vm.Targets.Single(t => t.Id == "gemini").Configured);
    }

    // ----- Sync: plain/dry-run explicit; force gated -----

    [Fact]
    public void Status_ForceSync_RequiresConfirmation()
    {
        var vm = new StatusViewModel(App());

        Assert.False(vm.ForceSyncPending);
        Assert.Null(vm.ConfirmForceSync()); // no-op without a prior request

        vm.RequestForceSync();
        Assert.True(vm.ForceSyncPending);
        var report = vm.ConfirmForceSync();
        Assert.NotNull(report);
        Assert.True(report!.ConfigValid);
        Assert.False(vm.ForceSyncPending);
    }

    [Fact]
    public void Status_DryRunSync_WritesNothing()
    {
        var vm = new StatusViewModel(App());

        var preview = vm.RunSync(dryRun: true);

        Assert.True(preview.DryRun);
        Assert.True(preview.AnyChanges);
        // Still drifted because the dry run wrote nothing.
        Assert.True(vm.Drift!.HasDrift);
    }

    // ----- Hooks: install gated -----

    [Fact]
    public void Hooks_Install_RequiresConfirmation()
    {
        var vm = new HooksViewModel(App());

        Assert.False(vm.InstallPending);
        Assert.Null(vm.ConfirmInstall()); // no-op without a prior request
        Assert.Null(vm.Result);

        vm.RequestInstall();
        Assert.True(vm.InstallPending);
        var result = vm.ConfirmInstall();
        Assert.NotNull(result);
        Assert.Equal(".githooks", result!.HooksPath);
        Assert.False(vm.InstallPending);
    }
}
