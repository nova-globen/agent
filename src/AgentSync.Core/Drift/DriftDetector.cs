using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;
using AgentSync.Core.Projections;

namespace AgentSync.Core.Drift;

public enum DriftKind
{
    /// <summary>The projection matches the canonical source.</summary>
    InSync,

    /// <summary>The target file or managed section does not exist.</summary>
    Missing,

    /// <summary>The projection exists and is unedited, but the canonical source changed.</summary>
    Outdated,

    /// <summary>The generated content was edited by hand.</summary>
    ManualEdit,
}

/// <summary>Drift state of a single planned projection.</summary>
public sealed record DriftItem(
    string SkillId,
    string TargetId,
    string RelativePath,
    DriftKind Kind,
    bool LockMismatch)
{
    public bool IsDrift => Kind != DriftKind.InSync || LockMismatch;
}

/// <summary>A lockfile entry that no longer corresponds to a planned projection.</summary>
public sealed record OrphanLockEntry(string Skill, string Target, string Path);

public sealed record DriftReport(
    bool ConfigValid,
    ValidationResult Validation,
    IReadOnlyList<DriftItem> Items,
    IReadOnlyList<OrphanLockEntry> Orphans,
    AgentPolicy Policy)
{
    public bool HasDrift =>
        !ConfigValid
        || Items.Any(i => i.IsDrift)
        || Orphans.Count > 0;

    public IEnumerable<DriftItem> Drifted => Items.Where(i => i.IsDrift);
}

/// <summary>
/// Compares canonical skills against their on-disk projections and the lockfile to
/// classify drift: missing, outdated (stale), manually edited, plus config errors,
/// missing lockfile entries, and orphaned lockfile entries.
/// </summary>
public sealed class DriftDetector
{
    private readonly RepoLayout _layout;
    private readonly ProjectionPlanner _planner;

    public DriftDetector(string repoRoot, ProjectionPlanner? planner = null)
    {
        _layout = new RepoLayout(repoRoot);
        _planner = planner ?? new ProjectionPlanner();
    }

    public DriftReport Detect()
    {
        var workspace = WorkspaceLoader.Load(_layout.RepoRoot);
        if (!workspace.IsValid)
        {
            return new DriftReport(false, workspace.Validation, Array.Empty<DriftItem>(), Array.Empty<OrphanLockEntry>(), new AgentPolicy());
        }

        var policy = workspace.Config!.Policy;

        var projections = _planner.Plan(workspace);
        var lockfile = Lockfile.Load(_layout.LockFile);

        var items = projections.Select(p => Classify(p, lockfile)).ToList();

        var plannedKeys = projections
            .Select(p => Lockfile.KeyFor(p.SkillId, p.TargetId))
            .ToHashSet(StringComparer.Ordinal);
        var orphans = lockfile.Projections
            .Where(kv => !plannedKeys.Contains(kv.Key))
            .Select(kv => new OrphanLockEntry(kv.Value.Skill, kv.Value.Target, kv.Value.Path))
            .ToList();

        return new DriftReport(true, workspace.Validation, items, orphans, policy);
    }

    private DriftItem Classify(Projection projection, Lockfile lockfile)
    {
        var absolutePath = RepoPath.Resolve(_layout.RepoRoot, projection.RelativePath);
        var desiredHash = ContentHasher.HashWithAssets(projection.Body, projection.AssetSourceDir);
        var lockEntry = lockfile.Get(projection.SkillId, projection.TargetId);
        var lockMismatch = lockEntry is null || lockEntry.Hash != desiredHash;

        string? currentBody;
        string? declaredHash;

        if (projection.Mode == ProjectionMode.SharedSection)
        {
            if (!File.Exists(absolutePath))
            {
                return new DriftItem(projection.SkillId, projection.TargetId, projection.RelativePath, DriftKind.Missing, lockMismatch);
            }

            var doc = MarkedDocument.Parse(File.ReadAllText(absolutePath));
            var section = doc.Find(projection.SkillId, projection.TargetId);
            if (section is null)
            {
                return new DriftItem(projection.SkillId, projection.TargetId, projection.RelativePath, DriftKind.Missing, lockMismatch);
            }

            currentBody = section.Body;
            declaredHash = section.DeclaredHash;
        }
        else
        {
            if (!File.Exists(absolutePath))
            {
                return new DriftItem(projection.SkillId, projection.TargetId, projection.RelativePath, DriftKind.Missing, lockMismatch);
            }

            // For whole-file targets, use the combined hash (body + any assets) to detect
            // InSync. For Outdated vs ManualEdit classification, compare the *body only*
            // against the lockfile hash (which now stores just the body hash). This means
            // a deleted or changed references/ file is Outdated (auto-restorable) rather
            // than ManualEdit (needs --force), because assets are managed projections, not
            // user-authored content.
            var targetRefsDir = projection.AssetSourceDir is not null
                ? Path.Combine(Path.GetDirectoryName(absolutePath)!, "references")
                : null;
            currentBody = File.ReadAllText(absolutePath);
            var currentBodyHash = ContentHasher.Hash(currentBody);
            var currentHashWithAssets = ContentHasher.HashWithAssets(currentBody, targetRefsDir);
            var declaredBodyHash = lockEntry?.Hash;  // body-only hash stored by ProjectionApplier

            // lockMismatch: the body hash we stored no longer matches what we'd write today.
            var desiredBodyHash = ContentHasher.Hash(projection.Body);
            var wholeLockMismatch = lockEntry is null || lockEntry.Hash != desiredBodyHash;

            var kindWhole =
                currentHashWithAssets == desiredHash ? DriftKind.InSync
                // Body unchanged since we wrote it → assets or canonical drifted (Outdated).
                : string.Equals(currentBodyHash, declaredBodyHash, StringComparison.Ordinal) ? DriftKind.Outdated
                : declaredBodyHash is null ? DriftKind.ManualEdit
                : DriftKind.ManualEdit;

            return new DriftItem(projection.SkillId, projection.TargetId, projection.RelativePath, kindWhole,
                wholeLockMismatch && kindWhole == DriftKind.InSync);
        }

        var currentHash = ContentHasher.Hash(currentBody);

        var kind =
            currentHash == desiredHash ? DriftKind.InSync
            : declaredHash is null ? DriftKind.ManualEdit
            : currentHash != declaredHash ? DriftKind.ManualEdit
            : DriftKind.Outdated;

        // When in sync, only flag a lock mismatch (e.g. lockfile entry deleted).
        return new DriftItem(projection.SkillId, projection.TargetId, projection.RelativePath, kind, lockMismatch && kind == DriftKind.InSync);
    }
}
