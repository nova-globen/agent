using AgentSync.Core;
using AgentSync.Core.Configuration;
using AgentSync.Core.Drift;
using AgentSync.Ui.Abstractions;

namespace AgentSync.Ui.Web.ViewModels;

/// <summary>
/// UI-state + actions for the Status / Drift screen, kept out of the Razor component so it is
/// unit-testable. A plain sync (including a dry-run preview) is an explicit, single-action
/// operation. A force sync overwrites manually edited projections and is therefore gated:
/// <see cref="ConfirmForceSync"/> does nothing unless <see cref="RequestForceSync"/> was called
/// first (the second confirmation step the UI renders).
/// </summary>
public sealed class StatusViewModel
{
    private readonly AgentSyncApp _app;

    public StatusViewModel(AgentSyncApp app)
    {
        _app = app;
        Refresh();
    }

    public StatusReport? Status { get; private set; }
    public DriftReport? Drift { get; private set; }
    public ValidationResult? Validation { get; private set; }
    public SyncReport? Sync { get; private set; }

    /// <summary>True only while a force sync is awaiting its confirmation step.</summary>
    public bool ForceSyncPending { get; private set; }

    public void Refresh()
    {
        Status = _app.Status();
        Drift = _app.Drift();
        Validation = _app.Validate();
    }

    /// <summary>Runs a normal sync (or a dry-run preview when <paramref name="dryRun"/> is true).</summary>
    public SyncReport RunSync(bool dryRun)
    {
        Sync = _app.Sync(force: false, dryRun: dryRun);
        ForceSyncPending = false;
        if (!dryRun)
        {
            Refresh();
        }

        return Sync;
    }

    /// <summary>Opens the force-sync confirmation. Writes nothing on its own.</summary>
    public void RequestForceSync() => ForceSyncPending = true;

    public void CancelForceSync() => ForceSyncPending = false;

    /// <summary>Force-syncs only when one was requested first; otherwise does nothing.</summary>
    public SyncReport? ConfirmForceSync()
    {
        if (!ForceSyncPending)
        {
            return null;
        }

        Sync = _app.Sync(force: true, dryRun: false);
        ForceSyncPending = false;
        Refresh();
        return Sync;
    }
}
