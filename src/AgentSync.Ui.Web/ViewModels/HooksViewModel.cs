using AgentSync.Core;
using AgentSync.Ui.Abstractions;

namespace AgentSync.Ui.Web.ViewModels;

/// <summary>
/// Actions for the Hooks / CI screen, kept out of the Razor component so it is unit-testable.
/// Installing hooks changes Git config and the working tree, so it is gated:
/// <see cref="ConfirmInstall"/> does nothing unless <see cref="RequestInstall"/> was called
/// first (the second confirmation step the UI renders).
/// </summary>
public sealed class HooksViewModel
{
    private readonly AgentSyncApp _app;

    public HooksViewModel(AgentSyncApp app) => _app = app;

    /// <summary>True only while an install is awaiting its confirmation step.</summary>
    public bool InstallPending { get; private set; }

    public InstallHooksResult? Result { get; private set; }

    /// <summary>Opens the install confirmation. Writes nothing on its own.</summary>
    public void RequestInstall() => InstallPending = true;

    public void CancelInstall() => InstallPending = false;

    /// <summary>Installs hooks only when one was requested first; otherwise does nothing.</summary>
    public InstallHooksResult? ConfirmInstall()
    {
        if (!InstallPending)
        {
            return null;
        }

        Result = _app.InstallHooks();
        InstallPending = false;
        return Result;
    }
}
