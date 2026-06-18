using AgentSync.Core.Projections;

namespace AgentSync.Core.Adapters;

public sealed record ApplyOutcome(Projection Projection, ProjectionChange Change, bool ManualEditDetected);

/// <summary>
/// Applies planned projections to disk safely and records their hashes in the lockfile.
/// Shared-section targets use agent-sync markers; whole-file targets compare against the
/// lockfile hash to detect (and refuse to clobber) manual edits. Pass <c>dryRun</c> to
/// classify changes without touching the filesystem or lockfile (used by <c>--check</c>).
/// </summary>
public sealed class ProjectionApplier
{
    private readonly string _repoRoot;

    public ProjectionApplier(string repoRoot) => _repoRoot = Path.GetFullPath(repoRoot);

    public ApplyOutcome Apply(Projection projection, Lockfile lockfile, bool force = false, bool dryRun = false)
    {
        var absolutePath = Path.Combine(_repoRoot, projection.RelativePath);

        var result = projection.Mode == ProjectionMode.SharedSection
            ? ApplySharedSection(absolutePath, projection, force, dryRun)
            : ApplyWholeFile(absolutePath, projection, lockfile, force, dryRun);

        if (!dryRun && result.Change is ProjectionChange.Created or ProjectionChange.Updated or ProjectionChange.Unchanged)
        {
            lockfile.Record(projection.SkillId, projection.TargetId, projection.RelativePath, result.Hash);
        }

        return new ApplyOutcome(projection, result.Change, result.ManualEditDetected);
    }

    public IReadOnlyList<ApplyOutcome> ApplyAll(
        IEnumerable<Projection> projections, Lockfile lockfile, bool force = false, bool dryRun = false)
        => projections.Select(p => Apply(p, lockfile, force, dryRun)).ToList();

    private static UpsertResult ApplySharedSection(string absolutePath, Projection projection, bool force, bool dryRun)
    {
        var existing = File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : string.Empty;
        var doc = MarkedDocument.Parse(existing);
        var result = doc.Upsert(projection.SkillId, projection.TargetId, projection.Body, force);

        if (result.Wrote && !dryRun)
        {
            var content = doc.Render();
            if (!content.EndsWith('\n'))
            {
                content += "\n";
            }

            WriteFile(absolutePath, content, alreadyTerminated: true);
        }

        return result;
    }

    private UpsertResult ApplyWholeFile(string absolutePath, Projection projection, Lockfile lockfile, bool force, bool dryRun)
    {
        var newHash = ContentHasher.Hash(projection.Body);

        if (!File.Exists(absolutePath))
        {
            if (!dryRun)
            {
                WriteFile(absolutePath, projection.Body);
            }

            return new UpsertResult(ProjectionChange.Created, newHash, ManualEditDetected: false);
        }

        var existing = File.ReadAllText(absolutePath);
        var currentHash = ContentHasher.Hash(existing);
        var lockEntry = lockfile.Get(projection.SkillId, projection.TargetId);
        var manuallyEdited = lockEntry is null || !ContentHasher.Matches(existing, lockEntry.Hash);

        if (currentHash == newHash)
        {
            return new UpsertResult(ProjectionChange.Unchanged, newHash, ManualEditDetected: false);
        }

        if (manuallyEdited && !force)
        {
            return new UpsertResult(ProjectionChange.SkippedManualEdit, currentHash, ManualEditDetected: true);
        }

        if (!dryRun)
        {
            WriteFile(absolutePath, projection.Body);
        }

        return new UpsertResult(ProjectionChange.Updated, newHash, ManualEditDetected: manuallyEdited);
    }

    private static void WriteFile(string absolutePath, string body, bool alreadyTerminated = false)
    {
        var dir = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var content = alreadyTerminated || body.EndsWith('\n') ? body : body + "\n";
        File.WriteAllText(absolutePath, content);
    }
}
