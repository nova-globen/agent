using AgentSync.Core.Configuration;
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
/// Projects canonical sub-agents (<c>.agent/agents/&lt;id&gt;/</c>) into agent-specific files
/// and detects drift. Always projects to Claude Code (<c>.claude/agents/&lt;id&gt;.md</c>).
/// Optionally projects to a TOML target (<c>toml_agent</c>) when configured in
/// <c>.agent/agent.yaml</c>. Each projection is a managed whole file; manual edits are
/// detected via the dedicated sub-agent lockfile (<c>.agent/agents.lock.json</c>) and
/// never overwritten unless <c>--force</c> is passed.
/// </summary>
public sealed class SubagentProjector
{
    private const string ClaudeAgentTarget = "claude_agent";
    private readonly RepoLayout _layout;
    private readonly AgentConfig? _config;

    public SubagentProjector(string repoRoot)
    {
        _layout = new RepoLayout(repoRoot);
        _config = TryLoadConfig(repoRoot);
    }

    private static AgentConfig? TryLoadConfig(string repoRoot)
    {
        var path = Path.Combine(repoRoot, RepoLayout.AgentDirName, RepoLayout.ConfigFileName);
        if (!File.Exists(path)) return null;
        try { return AgentConfig.Parse(File.ReadAllText(path)); }
        catch { return null; }
    }

    /// <summary>
    /// Returns the active agent projection targets: always <c>claude_agent</c>, plus
    /// <c>toml_agent</c> if it is enabled and has a configured path in <c>agent.yaml</c>.
    /// Each entry is (targetId, relativeDir).
    /// </summary>
    private IEnumerable<(string Target, string RelDir)> ActiveTargets()
    {
        yield return (ClaudeAgentTarget, RepoLayout.ClaudeAgentsDir);

        if (_config?.Targets.TryGetValue(TargetIds.TomlAgent, out var toml) == true
            && toml.Enabled
            && !string.IsNullOrWhiteSpace(toml.Path))
        {
            yield return (TargetIds.TomlAgent, toml.Path!.TrimEnd('/', '\\'));
        }
    }

    private string RenderForTarget(Subagent agent, string target) => target == ClaudeAgentTarget
        ? SubagentFiles.RenderProjection(agent)
        : TomlAgentRenderer.Render(agent);

    private static string ExtensionForTarget(string target) => target == ClaudeAgentTarget ? ".md" : ".toml";

    public SubagentSyncReport Sync(bool force, bool dryRun)
    {
        var agents = SubagentFiles.LoadAll(_layout);
        var lockfile = Lockfile.Load(_layout.AgentsLockFile);
        var outcomes = new List<SubagentOutcome>();
        var liveIds = new HashSet<string>(StringComparer.Ordinal);
        var activeTargets = ActiveTargets().ToList();

        foreach (var agent in agents)
        {
            liveIds.Add(agent.Id);

            foreach (var (target, relDir) in activeTargets)
            {
                var ext = ExtensionForTarget(target);
                var relPath = $"{relDir}/{agent.Id}{ext}";
                var absPath = RepoPath.Resolve(_layout.RepoRoot, relPath);
                var newContent = RenderForTarget(agent, target);
                var newHash = ContentHasher.Hash(newContent);
                var lockEntry = lockfile.Get(agent.Id, target);

                if (!File.Exists(absPath))
                {
                    if (!dryRun)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
                        File.WriteAllText(absPath, newContent);
                        lockfile.Record(agent.Id, target, relPath, newHash);
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
                        lockfile.Record(agent.Id, target, relPath, newHash);
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
                    lockfile.Record(agent.Id, target, relPath, newHash);
                }

                outcomes.Add(new SubagentOutcome(agent.Id, relPath, SubagentChange.Updated, manualEdit));
            }
        }

        // Prune lockfile entries for sub-agents that no longer exist or targets no longer active.
        if (!dryRun)
        {
            var activeTargetIds = new HashSet<string>(activeTargets.Select(t => t.Target), StringComparer.Ordinal);
            var stale = lockfile.Projections
                .Where(kv => activeTargetIds.Contains(kv.Value.Target) && !liveIds.Contains(kv.Value.Skill))
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

    /// <summary>
    /// Reconciles the projections for the given just-imported sub-agents so that importing one's
    /// own existing <c>.claude/agents/&lt;id&gt;.md</c> does not immediately register as drift.
    /// For each id whose projection file already exists, the file is rewritten in canonical form
    /// and its hash recorded in the lockfile (the user explicitly adopted it). Ids with no
    /// existing projection are left untouched for a normal <c>agent sync</c>.
    /// </summary>
    /// <returns>The ids whose projection was reconciled.</returns>
    public IReadOnlyList<string> ReconcileImported(IEnumerable<string> ids)
    {
        var wanted = new HashSet<string>(ids, StringComparer.Ordinal);
        var reconciled = new List<string>();
        if (wanted.Count == 0)
        {
            return reconciled;
        }

        var lockfile = Lockfile.Load(_layout.AgentsLockFile);
        var activeTargets = ActiveTargets().ToList();
        foreach (var agent in SubagentFiles.LoadAll(_layout).Where(a => wanted.Contains(a.Id)))
        {
            var anyReconciled = false;
            foreach (var (target, relDir) in activeTargets)
            {
                var ext = ExtensionForTarget(target);
                var relPath = $"{relDir}/{agent.Id}{ext}";
                var absPath = RepoPath.Resolve(_layout.RepoRoot, relPath);
                if (!File.Exists(absPath))
                {
                    continue;
                }

                var content = RenderForTarget(agent, target);
                File.WriteAllText(absPath, content);
                lockfile.Record(agent.Id, target, relPath, ContentHasher.Hash(content));
                anyReconciled = true;
            }

            if (anyReconciled)
            {
                reconciled.Add(agent.Id);
            }
        }

        if (reconciled.Count > 0)
        {
            lockfile.Save(_layout.AgentsLockFile);
        }

        return reconciled;
    }

    public IReadOnlyList<SubagentDrift> Detect()
    {
        var drift = new List<SubagentDrift>();
        var agents = SubagentFiles.LoadAll(_layout);
        var lockfile = Lockfile.Load(_layout.AgentsLockFile);
        var liveIds = new HashSet<string>(StringComparer.Ordinal);
        var activeTargets = ActiveTargets().ToList();
        var activeTargetIds = new HashSet<string>(activeTargets.Select(t => t.Target), StringComparer.Ordinal);

        foreach (var agent in agents)
        {
            liveIds.Add(agent.Id);

            foreach (var (target, relDir) in activeTargets)
            {
                var ext = ExtensionForTarget(target);
                var relPath = $"{relDir}/{agent.Id}{ext}";
                var absPath = RepoPath.Resolve(_layout.RepoRoot, relPath);
                var newContent = RenderForTarget(agent, target);
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

                var lockEntry = lockfile.Get(agent.Id, target);
                var kind = lockEntry is not null && string.Equals(existingHash, lockEntry.Hash, StringComparison.Ordinal)
                    ? SubagentDriftKind.Outdated
                    : SubagentDriftKind.ManualEdit;
                drift.Add(new SubagentDrift(agent.Id, relPath, kind));
            }
        }

        foreach (var kv in lockfile.Projections.Where(kv => activeTargetIds.Contains(kv.Value.Target)))
        {
            if (!liveIds.Contains(kv.Value.Skill))
            {
                drift.Add(new SubagentDrift(kv.Value.Skill, kv.Value.Path, SubagentDriftKind.Orphan));
            }
        }

        return drift;
    }
}
