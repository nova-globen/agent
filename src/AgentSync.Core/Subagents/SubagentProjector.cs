using AgentSync.Core.Projections;

namespace AgentSync.Core.Subagents;

/// <summary>What happened to one sub-agent projection during a sync.</summary>
public enum SubagentChange
{
    Created,
    Updated,
    UpToDate,
    SkippedManualEdit,
}

/// <summary>How a sub-agent projection has drifted from its canonical source.</summary>
public enum SubagentDriftKind
{
    Missing,
    Outdated,
    ManualEdit,
    Orphan,
}

public sealed record SubagentOutcome(string Id, string Path, SubagentChange Change, bool ManualEditDetected);

public sealed record SubagentDrift(string Id, string Path, SubagentDriftKind Kind);

public sealed record SubagentSyncReport(IReadOnlyList<SubagentOutcome> Outcomes)
{
    public bool AnyChanges => Outcomes.Any(o => o.Change is SubagentChange.Created or SubagentChange.Updated);
    public bool AnySkippedManualEdits => Outcomes.Any(o => o.Change == SubagentChange.SkippedManualEdit);
}

/// <summary>
/// Projects canonical sub-agents (<c>.agent/agents/&lt;id&gt;/</c>) into Claude Code sub-agent
/// files (<c>.claude/agents/&lt;id&gt;.md</c>) and detects drift. Each projection is a managed
/// whole file; manual edits are detected via the dedicated sub-agent lockfile
/// (<c>.agent/agents.lock.json</c>) and never overwritten unless <c>--force</c> is passed.
/// </summary>
public sealed class SubagentProjector
{
    private const string Target = "claude_agent";
    private readonly RepoLayout _layout;

    public SubagentProjector(string repoRoot) => _layout = new RepoLayout(repoRoot);

    public SubagentSyncReport Sync(bool force, bool dryRun)
    {
        var agents = SubagentFiles.LoadAll(_layout);
        var lockfile = Lockfile.Load(_layout.AgentsLockFile);
        var outcomes = new List<SubagentOutcome>();
        var liveIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var agent in agents)
        {
            liveIds.Add(agent.Id);
            var relPath = $"{RepoLayout.ClaudeAgentsDir}/{agent.Id}.md";
            var absPath = RepoPath.Resolve(_layout.RepoRoot, relPath);
            var newContent = SubagentFiles.RenderProjection(agent);
            var newHash = ContentHasher.Hash(newContent);
            var lockEntry = lockfile.Get(agent.Id, Target);

            if (!File.Exists(absPath))
            {
                if (!dryRun)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
                    File.WriteAllText(absPath, newContent);
                    lockfile.Record(agent.Id, Target, relPath, newHash);
                }

                outcomes.Add(new SubagentOutcome(agent.Id, relPath, SubagentChange.Created, false));
                continue;
            }

            var existing = File.ReadAllText(absPath);
            var existingHash = ContentHasher.Hash(existing);
            if (string.Equals(existingHash, newHash, StringComparison.Ordinal))
            {
                if (!dryRun)
                {
                    lockfile.Record(agent.Id, Target, relPath, newHash);
                }

                outcomes.Add(new SubagentOutcome(agent.Id, relPath, SubagentChange.UpToDate, false));
                continue;
            }

            // Existing differs from canonical. If it matches the recorded hash it is our own
            // prior output and can be updated; otherwise it was edited by hand.
            var manualEdit = lockEntry is null || !string.Equals(existingHash, lockEntry.Hash, StringComparison.Ordinal);
            if (manualEdit && !force)
            {
                outcomes.Add(new SubagentOutcome(agent.Id, relPath, SubagentChange.SkippedManualEdit, true));
                continue;
            }

            if (!dryRun)
            {
                File.WriteAllText(absPath, newContent);
                lockfile.Record(agent.Id, Target, relPath, newHash);
            }

            outcomes.Add(new SubagentOutcome(agent.Id, relPath, SubagentChange.Updated, manualEdit));
        }

        // Prune lockfile entries for sub-agents that no longer exist.
        if (!dryRun)
        {
            var stale = lockfile.Projections
                .Where(kv => string.Equals(kv.Value.Target, Target, StringComparison.Ordinal) && !liveIds.Contains(kv.Value.Skill))
                .Select(kv => kv.Key)
                .ToList();
            foreach (var key in stale)
            {
                lockfile.Projections.Remove(key);
            }

            if (agents.Count > 0 || lockfile.Projections.Count > 0 || File.Exists(_layout.AgentsLockFile))
            {
                lockfile.Save(_layout.AgentsLockFile);
            }
        }

        return new SubagentSyncReport(outcomes);
    }

    public IReadOnlyList<SubagentDrift> Detect()
    {
        var drift = new List<SubagentDrift>();
        var agents = SubagentFiles.LoadAll(_layout);
        var lockfile = Lockfile.Load(_layout.AgentsLockFile);
        var liveIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var agent in agents)
        {
            liveIds.Add(agent.Id);
            var relPath = $"{RepoLayout.ClaudeAgentsDir}/{agent.Id}.md";
            var absPath = RepoPath.Resolve(_layout.RepoRoot, relPath);
            var newContent = SubagentFiles.RenderProjection(agent);
            var newHash = ContentHasher.Hash(newContent);

            if (!File.Exists(absPath))
            {
                drift.Add(new SubagentDrift(agent.Id, relPath, SubagentDriftKind.Missing));
                continue;
            }

            var existingHash = ContentHasher.Hash(File.ReadAllText(absPath));
            if (string.Equals(existingHash, newHash, StringComparison.Ordinal))
            {
                continue;
            }

            var lockEntry = lockfile.Get(agent.Id, Target);
            var kind = lockEntry is not null && string.Equals(existingHash, lockEntry.Hash, StringComparison.Ordinal)
                ? SubagentDriftKind.Outdated
                : SubagentDriftKind.ManualEdit;
            drift.Add(new SubagentDrift(agent.Id, relPath, kind));
        }

        foreach (var kv in lockfile.Projections.Where(kv => string.Equals(kv.Value.Target, Target, StringComparison.Ordinal)))
        {
            if (!liveIds.Contains(kv.Value.Skill))
            {
                drift.Add(new SubagentDrift(kv.Value.Skill, kv.Value.Path, SubagentDriftKind.Orphan));
            }
        }

        return drift;
    }
}
