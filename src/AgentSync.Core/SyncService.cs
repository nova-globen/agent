using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;
using AgentSync.Core.Projections;

namespace AgentSync.Core;

public sealed record SyncReport(
    bool ConfigValid,
    ValidationResult Validation,
    IReadOnlyList<ApplyOutcome> Outcomes,
    bool DryRun)
{
    /// <summary>A manual edit was detected on a projection (whether or not it was
    /// overwritten). With <c>--force</c> these are rewritten rather than skipped.</summary>
    public bool AnyManualEdits => Outcomes.Any(o => o.ManualEditDetected);

    /// <summary>A manually edited projection was left untouched because <c>--force</c> was
    /// not passed. This is the unresolved-problem case (forced edits are rewritten).</summary>
    public bool AnySkippedManualEdits =>
        Outcomes.Any(o => o.Change == ProjectionChange.SkippedManualEdit);

    public bool AnyChanges => Outcomes.Any(o =>
        o.Change is ProjectionChange.Created or ProjectionChange.Updated);

    public bool HasProblems => !ConfigValid || AnySkippedManualEdits;
}

/// <summary>
/// Orchestrates a sync: load + validate the workspace, plan projections, apply them
/// (or preview with <paramref name="dryRun"/>), and persist the lockfile.
/// </summary>
public sealed class SyncService
{
    private readonly RepoLayout _layout;
    private readonly ProjectionPlanner _planner;

    public SyncService(string repoRoot, ProjectionPlanner? planner = null)
    {
        _layout = new RepoLayout(repoRoot);
        _planner = planner ?? new ProjectionPlanner();
    }

    public SyncReport Run(bool force = false, bool dryRun = false)
    {
        var workspace = WorkspaceLoader.Load(_layout.RepoRoot);
        if (!workspace.IsValid)
        {
            return new SyncReport(false, workspace.Validation, Array.Empty<ApplyOutcome>(), dryRun);
        }

        var projections = _planner.Plan(workspace);
        var lockfile = Lockfile.Load(_layout.LockFile);
        var applier = new ProjectionApplier(_layout.RepoRoot);

        var outcomes = applier.ApplyAll(projections, lockfile, force, dryRun);

        if (!dryRun)
        {
            lockfile.Save(_layout.LockFile);
        }

        return new SyncReport(true, workspace.Validation, outcomes, dryRun);
    }
}
