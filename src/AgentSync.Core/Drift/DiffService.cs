using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;
using AgentSync.Core.Projections;

namespace AgentSync.Core.Drift;

public sealed record DiffEntry(
    string SkillId,
    string TargetId,
    string RelativePath,
    DriftKind Kind,
    string Diff);

public sealed record DiffReport(
    bool ConfigValid,
    ValidationResult Validation,
    IReadOnlyList<DiffEntry> Entries)
{
    public bool HasDifferences => Entries.Count > 0;
}

/// <summary>
/// Produces canonical-to-projection line diffs for every projection that drifted
/// (missing, outdated, or manually edited), reusing <see cref="DriftDetector"/> for
/// classification so status and diff never disagree.
/// </summary>
public sealed class DiffService
{
    private readonly RepoLayout _layout;
    private readonly ProjectionPlanner _planner;

    public DiffService(string repoRoot, ProjectionPlanner? planner = null)
    {
        _layout = new RepoLayout(repoRoot);
        _planner = planner ?? new ProjectionPlanner();
    }

    public DiffReport Run()
    {
        var workspace = WorkspaceLoader.Load(_layout.RepoRoot);
        if (!workspace.IsValid)
        {
            return new DiffReport(false, workspace.Validation, Array.Empty<DiffEntry>());
        }

        var drift = new DriftDetector(_layout.RepoRoot, _planner).Detect();
        var projectionsByKey = _planner.Plan(workspace)
            .ToDictionary(p => Lockfile.KeyFor(p.SkillId, p.TargetId), StringComparer.Ordinal);

        var entries = new List<DiffEntry>();
        foreach (var item in drift.Items.Where(i => i.Kind != DriftKind.InSync))
        {
            if (!projectionsByKey.TryGetValue(Lockfile.KeyFor(item.SkillId, item.TargetId), out var projection))
            {
                continue;
            }

            var current = ReadCurrent(projection);
            var diff = LineDiff.Compute(current ?? string.Empty, projection.Body);
            entries.Add(new DiffEntry(item.SkillId, item.TargetId, item.RelativePath, item.Kind, diff));
        }

        return new DiffReport(true, workspace.Validation, entries);
    }

    private string? ReadCurrent(Projection projection)
    {
        var absolutePath = RepoPath.Resolve(_layout.RepoRoot, projection.RelativePath);
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        if (projection.Mode == ProjectionMode.SharedSection)
        {
            var doc = MarkedDocument.Parse(File.ReadAllText(absolutePath));
            return doc.Find(projection.SkillId, projection.TargetId)?.Body;
        }

        return File.ReadAllText(absolutePath);
    }
}
