using AgentSync.Core.Authoring;
using AgentSync.Core.Configuration;
using AgentSync.Ui.Abstractions;

namespace AgentSync.Ui.Web.ViewModels;

/// <summary>
/// UI-state + mutation flow for the Skills screen, kept out of the Razor component so it is
/// unit-testable. Add and Save are explicit, single-action file-writing operations. Delete is
/// destructive and gated: <see cref="ConfirmDelete"/> does nothing unless a delete was first
/// requested via <see cref="RequestDelete"/> (the second confirmation step the UI renders).
/// </summary>
public sealed class SkillsViewModel
{
    private readonly AgentSyncApp _app;

    public SkillsViewModel(AgentSyncApp app)
    {
        _app = app;
        Refresh();
    }

    public IReadOnlyList<Skill> Skills { get; private set; } = Array.Empty<Skill>();

    // ----- Add (explicit, non-destructive) -----
    public string AddId { get; set; } = string.Empty;
    public string AddName { get; set; } = string.Empty;
    public string AddDescription { get; set; } = string.Empty;
    public string AddVersion { get; set; } = string.Empty;
    public AuthoringResult? AddResult { get; private set; }

    // ----- Edit (explicit, non-destructive) -----
    public string? EditId { get; private set; }
    public string EditName { get; set; } = string.Empty;
    public string EditDescription { get; set; } = string.Empty;
    public string EditVersion { get; set; } = string.Empty;
    public string EditBodyFile { get; set; } = string.Empty;
    public Dictionary<string, bool> EditTargets { get; private set; } = new(StringComparer.Ordinal);
    public AuthoringResult? EditResult { get; private set; }

    // ----- Delete (destructive — requires confirmation) -----
    /// <summary>Non-null only while a delete is awaiting its confirmation step.</summary>
    public string? PendingDeleteId { get; private set; }
    public bool DeleteDryRun { get; set; }
    public bool DeleteForce { get; set; }
    public AuthoringResult? DeleteResult { get; private set; }

    public void Refresh() => Skills = _app.ListSkills();

    public AuthoringResult Add()
    {
        AddResult = _app.AddSkill(AddId.Trim(), Blank(AddName), Blank(AddDescription), Blank(AddVersion), targets: null);
        if (AddResult.Status == AuthoringStatus.Ok)
        {
            AddId = AddName = AddDescription = AddVersion = string.Empty;
            Refresh();
        }

        return AddResult;
    }

    public void BeginEdit(Skill skill)
    {
        PendingDeleteId = null;
        EditId = skill.Id;
        EditResult = null;
        EditName = skill.Manifest.Name ?? string.Empty;
        EditDescription = skill.Manifest.Description ?? string.Empty;
        EditVersion = skill.Manifest.Version ?? string.Empty;
        EditBodyFile = string.Empty;
        EditTargets = TargetIds.Ordered.ToDictionary(
            t => t,
            t => skill.EnabledTargets.Contains(t),
            StringComparer.Ordinal);
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

        var current = Skills.FirstOrDefault(s => s.Id == EditId);
        var enabled = current?.EnabledTargets.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        var enable = EditTargets.Where(kv => kv.Value && !enabled.Contains(kv.Key)).Select(kv => kv.Key).ToList();
        var disable = EditTargets.Where(kv => !kv.Value && enabled.Contains(kv.Key)).Select(kv => kv.Key).ToList();

        EditResult = _app.EditSkill(
            EditId,
            Blank(EditName),
            Blank(EditDescription),
            Blank(EditVersion),
            Blank(EditBodyFile),
            enable.Count > 0 ? enable : null,
            disable.Count > 0 ? disable : null);

        if (EditResult.Status == AuthoringStatus.Ok)
        {
            Refresh();
        }

        return EditResult;
    }

    /// <summary>Opens the delete confirmation for a skill. Writes nothing on its own.</summary>
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

    /// <summary>
    /// Performs the delete — but only when a delete was requested first (the confirmation step).
    /// Returns null and does nothing when no delete is pending.
    /// </summary>
    public AuthoringResult? ConfirmDelete()
    {
        if (PendingDeleteId is null)
        {
            return null;
        }

        DeleteResult = _app.DeleteSkill(PendingDeleteId, DeleteForce, DeleteDryRun);
        if (DeleteResult.Status == AuthoringStatus.Ok && !DeleteDryRun)
        {
            PendingDeleteId = null;
            Refresh();
        }

        return DeleteResult;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
