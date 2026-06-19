using AgentSync.Core.Authoring;
using AgentSync.Ui.Abstractions;

namespace AgentSync.Ui.Web.ViewModels;

/// <summary>
/// UI-state + mutation flow for the Targets screen, kept out of the Razor component so it is
/// unit-testable. Add and Save are explicit, single-action file-writing operations. Delete is
/// destructive and gated: <see cref="ConfirmDelete"/> does nothing unless a delete was first
/// requested via <see cref="RequestDelete"/>.
/// </summary>
public sealed class TargetsViewModel
{
    private readonly AgentSyncApp _app;

    public TargetsViewModel(AgentSyncApp app)
    {
        _app = app;
        Refresh();
    }

    public IReadOnlyList<TargetView> Targets { get; private set; } = Array.Empty<TargetView>();

    // ----- Add (explicit, non-destructive) -----
    public string AddId { get; set; } = string.Empty;
    public string AddPath { get; set; } = string.Empty;
    public bool AddEnabled { get; set; } = true;
    public AuthoringResult? AddResult { get; private set; }

    // ----- Edit (explicit, non-destructive) -----
    public string? EditId { get; private set; }
    public string EditPath { get; set; } = string.Empty;
    public bool EditEnabled { get; set; }
    public AuthoringResult? EditResult { get; private set; }

    // ----- Delete (destructive — requires confirmation) -----
    public string? PendingDeleteId { get; private set; }
    public bool DeleteDryRun { get; set; }
    public bool DeleteForce { get; set; }
    public AuthoringResult? DeleteResult { get; private set; }

    public void Refresh() => Targets = _app.ListTargets();

    public AuthoringResult Add()
    {
        AddResult = _app.AddTarget(AddId.Trim(), Blank(AddPath), AddEnabled);
        if (AddResult.Status == AuthoringStatus.Ok)
        {
            AddId = string.Empty;
            AddPath = string.Empty;
            AddEnabled = true;
            Refresh();
        }

        return AddResult;
    }

    public void BeginEdit(TargetView target)
    {
        PendingDeleteId = null;
        EditId = target.Id;
        EditPath = target.Path ?? string.Empty;
        EditEnabled = target.Enabled;
        EditResult = null;
    }

    public void CancelEdit()
    {
        EditId = null;
        EditResult = null;
    }

    public AuthoringResult? SaveEdit()
    {
        if (EditId is null)
        {
            return null;
        }

        EditResult = _app.EditTarget(EditId, Blank(EditPath), EditEnabled);
        if (EditResult.Status == AuthoringStatus.Ok)
        {
            Refresh();
        }

        return EditResult;
    }

    /// <summary>Opens the delete confirmation for a target. Writes nothing on its own.</summary>
    public void RequestDelete(string id)
    {
        EditId = null;
        PendingDeleteId = id;
        DeleteDryRun = false;
        DeleteForce = false;
        DeleteResult = null;
    }

    public void CancelDelete()
    {
        PendingDeleteId = null;
        DeleteResult = null;
    }

    /// <summary>Performs the delete only when one was requested first; otherwise does nothing.</summary>
    public AuthoringResult? ConfirmDelete()
    {
        if (PendingDeleteId is null)
        {
            return null;
        }

        DeleteResult = _app.DeleteTarget(PendingDeleteId, DeleteForce, DeleteDryRun);
        if (DeleteResult.Status == AuthoringStatus.Ok && !DeleteDryRun)
        {
            PendingDeleteId = null;
            Refresh();
        }

        return DeleteResult;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
