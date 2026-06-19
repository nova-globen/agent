using AgentSync.Core.Configuration;
using AgentSync.Core.Projections;

namespace AgentSync.Core.Authoring;

/// <summary>
/// Adds, edits, and removes projection targets in <c>.agent/agent.yaml</c>. Target ids
/// must be known adapter ids; paths must be safe (<see cref="RepoPath"/>). The config is
/// round-tripped through <see cref="ConfigYaml"/> (comments are not preserved). Deleting
/// a target prunes its lockfile entries.
/// </summary>
public sealed class TargetWriter
{
    internal const string CommentNote = "note: comments in agent.yaml are not preserved by edits.";

    private readonly RepoLayout _layout;

    public TargetWriter(string repoRoot) => _layout = new RepoLayout(repoRoot);

    public AuthoringResult Add(string id, string? path, bool enabled)
    {
        if (!TargetIds.IsKnown(id))
        {
            return UnknownTarget(id);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return AuthoringResult.Fail(AuthoringStatus.InvalidUsage, "target add requires --path.");
        }

        if (!RepoPath.IsSafeRelative(path, out var pathError))
        {
            return AuthoringResult.Fail(AuthoringStatus.UnsafePath, $"Unsafe path: {pathError}");
        }

        if (!TryLoad(out var config, out var failure))
        {
            return failure!;
        }

        if (config!.Targets.ContainsKey(id))
        {
            return AuthoringResult.Fail(AuthoringStatus.Conflict,
                $"Target '{id}' is already configured. Use 'agent target edit {id}'.");
        }

        config.Targets[id] = new TargetSetting { Enabled = enabled, Path = path };
        Save(config);

        return Validated(new[] { $"added target {id} (enabled={enabled}, path={path})", CommentNote });
    }

    public AuthoringResult Edit(string id, string? path, bool? enabled)
    {
        if (!TargetIds.IsKnown(id))
        {
            return UnknownTarget(id);
        }

        if (path is null && enabled is null)
        {
            return AuthoringResult.Fail(AuthoringStatus.InvalidUsage, "Nothing to edit. Pass --path and/or --enabled.");
        }

        if (path is not null && !RepoPath.IsSafeRelative(path, out var pathError))
        {
            return AuthoringResult.Fail(AuthoringStatus.UnsafePath, $"Unsafe path: {pathError}");
        }

        if (!TryLoad(out var config, out var failure))
        {
            return failure!;
        }

        if (!config!.Targets.TryGetValue(id, out var setting))
        {
            return AuthoringResult.Fail(AuthoringStatus.NotFound,
                $"Target '{id}' is not configured. Use 'agent target add {id}'.");
        }

        var changes = new List<string>();
        if (path is not null)
        {
            setting.Path = path;
            changes.Add($"path = {path}");
        }

        if (enabled is not null)
        {
            setting.Enabled = enabled.Value;
            changes.Add($"enabled = {enabled.Value}");
            if (!enabled.Value)
            {
                var entries = LockEntriesForTarget(id);
                if (entries.Count > 0)
                {
                    changes.Add($"warning: {entries.Count} existing projection(s) for '{id}' will no longer be planned and become drift until removed or re-synced.");
                }
            }
        }

        Save(config);
        changes.Add(CommentNote);
        return Validated(changes);
    }

    public AuthoringResult Delete(string id, bool force, bool dryRun)
    {
        if (!TargetIds.IsKnown(id))
        {
            return UnknownTarget(id);
        }

        if (!TryLoad(out var config, out var failure))
        {
            return failure!;
        }

        if (!config!.Targets.ContainsKey(id))
        {
            return AuthoringResult.Fail(AuthoringStatus.NotFound, $"Target '{id}' is not configured.");
        }

        var entries = LockEntriesForTarget(id);
        if (entries.Count > 0 && !force)
        {
            var plan = entries.Select(e => $"lockfile entry {e.Value.Skill}/{e.Value.Target} ({e.Value.Path})").ToList();
            return new AuthoringResult(
                AuthoringStatus.Blocked,
                plan,
                Array.Empty<ValidationMessage>(),
                dryRun,
                $"Target '{id}' has {entries.Count} projection(s). Re-run with --force to remove the target and prune its lockfile entries; generated files/sections remain and should be cleaned up or re-synced.");
        }

        var changes = new List<string> { $"remove target {id}" };
        foreach (var e in entries)
        {
            changes.Add($"prune lockfile entry {e.Value.Skill}/{e.Value.Target}");
        }

        changes.Add(CommentNote);

        if (dryRun)
        {
            return new AuthoringResult(AuthoringStatus.Ok, changes, Array.Empty<ValidationMessage>(), DryRun: true);
        }

        config.Targets.Remove(id);
        Save(config);

        if (entries.Count > 0)
        {
            var lockfile = Lockfile.Load(_layout.LockFile);
            foreach (var e in entries)
            {
                lockfile.Projections.Remove(e.Key);
            }

            lockfile.Save(_layout.LockFile);
        }

        return Validated(changes);
    }

    private List<KeyValuePair<string, LockEntry>> LockEntriesForTarget(string id)
        => Lockfile.Load(_layout.LockFile).Projections
            .Where(kv => string.Equals(kv.Value.Target, id, StringComparison.Ordinal))
            .ToList();

    private bool TryLoad(out AgentConfig? config, out AuthoringResult? failure)
    {
        config = null;
        failure = null;

        if (!File.Exists(_layout.ConfigFile))
        {
            failure = AuthoringResult.Fail(AuthoringStatus.NotFound,
                "No .agent/agent.yaml found. Run 'agent init' first.");
            return false;
        }

        try
        {
            config = AgentConfig.Parse(File.ReadAllText(_layout.ConfigFile));
            // A YAML `targets:`/`policy:` key with an empty value deserializes to null,
            // which overrides the property initializers; normalize so mutation is safe.
            config.Targets ??= new Dictionary<string, TargetSetting>(StringComparer.Ordinal);
            config.Policy ??= new AgentPolicy();
            return true;
        }
        catch (ConfigParseException ex)
        {
            failure = AuthoringResult.Fail(AuthoringStatus.Invalid, $"agent.yaml: {ex.Message}");
            return false;
        }
    }

    private void Save(AgentConfig config)
    {
        var text = ConfigYaml.Render(config);
        if (!text.EndsWith('\n'))
        {
            text += "\n";
        }

        File.WriteAllText(_layout.ConfigFile, text);
    }

    private static AuthoringResult UnknownTarget(string id)
        => AuthoringResult.Fail(AuthoringStatus.InvalidUsage,
            $"Unknown target '{id}'. Known targets: {string.Join(", ", TargetIds.Ordered)}.");

    private AuthoringResult Validated(IReadOnlyList<string> changes)
    {
        var workspace = WorkspaceLoader.Load(_layout.RepoRoot);
        var errors = workspace.Validation.Messages.Where(m => m.Severity == ValidationSeverity.Error).ToList();
        var status = errors.Count > 0 ? AuthoringStatus.Invalid : AuthoringStatus.Ok;
        return new AuthoringResult(status, changes, errors, RecommendSync: true);
    }
}
