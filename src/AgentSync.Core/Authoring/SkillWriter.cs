using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;
using AgentSync.Core.Import;
using AgentSync.Core.Projections;

namespace AgentSync.Core.Authoring;

/// <summary>
/// Creates, edits, and deletes canonical skills under <c>.agent/skills/&lt;id&gt;/</c>.
/// All writes go through <see cref="SkillFiles"/> (deterministic output via
/// <see cref="Yaml.Scalar"/>) and every mutation re-validates the workspace. Deletes
/// prune the skill's lockfile entries so they do not become orphans.
/// </summary>
public sealed class SkillWriter
{
    private const string DefaultBody = "## When to use\n\nDescribe when an agent should use this skill.\n";

    private readonly RepoLayout _layout;

    public SkillWriter(string repoRoot) => _layout = new RepoLayout(repoRoot);

    public AuthoringResult Add(string id, string? name, string? description, string? version, IReadOnlyList<string>? targets)
    {
        if (!IdInference.IsValid(id))
        {
            return AuthoringResult.Fail(AuthoringStatus.InvalidUsage,
                $"Skill id '{id}' must be lowercase kebab-case (a-z, 0-9, hyphens).");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return AuthoringResult.Fail(AuthoringStatus.InvalidUsage, "skill add requires --name.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return AuthoringResult.Fail(AuthoringStatus.InvalidUsage, "skill add requires --description.");
        }

        if (TryRejectUnknownTargets(targets, out var failure))
        {
            return failure!;
        }

        if (SkillFiles.Exists(_layout, id))
        {
            return AuthoringResult.Fail(AuthoringStatus.Conflict,
                $"Skill '{id}' already exists. Use 'agent skill edit {id}' to change it.");
        }

        var enabled = targets is { Count: > 0 } ? targets : TargetIds.Ordered;
        var manifestYaml = SkillFiles.RenderManifestYaml(id, name!.Trim(), description!.Trim(), version?.Trim() ?? "0.1.0", enabled);

        SkillFiles.Write(_layout, id, manifestYaml, DefaultBody);

        return Validated(AuthoringStatus.Ok,
            new[]
            {
                $"created {_layout.Relative(Path.Combine(SkillFiles.SkillDir(_layout, id), "skill.yaml"))}",
                $"created {_layout.Relative(Path.Combine(SkillFiles.SkillDir(_layout, id), "SKILL.md"))}",
            },
            recommendSync: true);
    }

    public AuthoringResult Edit(
        string id,
        string? name,
        string? description,
        string? version,
        string? bodyFile,
        IReadOnlyList<string>? enableTargets,
        IReadOnlyList<string>? disableTargets)
    {
        var dir = SkillFiles.SkillDir(_layout, id);
        if (!Directory.Exists(dir))
        {
            return AuthoringResult.Fail(AuthoringStatus.NotFound, $"Skill '{id}' does not exist.");
        }

        var manifestPath = Path.Combine(dir, "skill.yaml");
        var bodyPath = Path.Combine(dir, "SKILL.md");

        SkillManifest manifest;
        try
        {
            manifest = File.Exists(manifestPath) ? SkillManifest.Parse(File.ReadAllText(manifestPath)) : new SkillManifest();
        }
        catch (ConfigParseException ex)
        {
            return AuthoringResult.Fail(AuthoringStatus.Invalid, $"{id}/skill.yaml: {ex.Message}");
        }

        manifest.Id = id;
        var body = File.Exists(bodyPath) ? File.ReadAllText(bodyPath) : string.Empty;
        var changes = new List<string>();

        if (name is not null)
        {
            manifest.Name = name.Trim();
            changes.Add($"name = \"{manifest.Name}\"");
        }

        if (description is not null)
        {
            manifest.Description = description.Trim();
            changes.Add($"description = \"{manifest.Description}\"");
        }

        if (version is not null)
        {
            manifest.Version = version.Trim();
            changes.Add($"version = \"{manifest.Version}\"");
        }

        if (bodyFile is not null)
        {
            var absolute = BodyFile.Resolve(_layout.RepoRoot, bodyFile);
            if (!File.Exists(absolute))
            {
                return AuthoringResult.Fail(AuthoringStatus.InvalidUsage, $"Body file '{bodyFile}' does not exist.");
            }

            var name2 = string.IsNullOrWhiteSpace(manifest.Name) ? id : manifest.Name!;
            body = SkillContent.StripRedundantHeading(File.ReadAllText(absolute), name2);
            changes.Add($"body <- {bodyFile}");
        }

        foreach (var t in enableTargets ?? Array.Empty<string>())
        {
            if (!TargetIds.IsKnown(t))
            {
                return AuthoringResult.Fail(AuthoringStatus.InvalidUsage, $"Unknown target '{t}'.");
            }

            manifest.Targets[t] = new SkillTargetSetting { Enabled = true };
            changes.Add($"enabled target {t}");
        }

        foreach (var t in disableTargets ?? Array.Empty<string>())
        {
            if (!TargetIds.IsKnown(t))
            {
                return AuthoringResult.Fail(AuthoringStatus.InvalidUsage, $"Unknown target '{t}'.");
            }

            manifest.Targets.Remove(t);
            changes.Add($"disabled target {t}");
        }

        if (changes.Count == 0)
        {
            return AuthoringResult.Fail(AuthoringStatus.InvalidUsage,
                "Nothing to edit. Pass --name, --description, --version, --body-file, --enable, or --disable.");
        }

        var enabledTargets = manifest.Targets.Where(kv => kv.Value.Enabled).Select(kv => kv.Key);
        var manifestYaml = SkillFiles.RenderManifestYaml(
            id,
            manifest.Name ?? id,
            manifest.Description ?? string.Empty,
            string.IsNullOrWhiteSpace(manifest.Version) ? "0.1.0" : manifest.Version!,
            enabledTargets);
        SkillFiles.Write(_layout, id, manifestYaml, body);

        return Validated(AuthoringStatus.Ok, changes, recommendSync: true);
    }

    public AuthoringResult Delete(string id, bool force, bool dryRun)
    {
        var dir = SkillFiles.SkillDir(_layout, id);
        if (!Directory.Exists(dir))
        {
            return AuthoringResult.Fail(AuthoringStatus.NotFound, $"Skill '{id}' does not exist.");
        }

        var lockfile = Lockfile.Load(_layout.LockFile);
        var lockEntries = lockfile.Projections
            .Where(kv => string.Equals(kv.Value.Skill, id, StringComparison.Ordinal))
            .ToList();

        var changes = new List<string>();
        var plan = new List<string>();
        foreach (var entry in lockEntries)
        {
            plan.Add($"lockfile entry {entry.Value.Skill}/{entry.Value.Target} ({entry.Value.Path})");
        }

        if (lockEntries.Count > 0 && !force)
        {
            var blocked = new AuthoringResult(
                AuthoringStatus.Blocked,
                plan,
                Array.Empty<ValidationMessage>(),
                dryRun,
                $"Skill '{id}' has {lockEntries.Count} projection(s). Re-run with --force to delete it and prune its lockfile entries; generated sections in shared files will remain and should be cleaned up or re-synced.");
            return blocked;
        }

        changes.Add($"delete {_layout.Relative(dir)}/");

        if (!dryRun)
        {
            Directory.Delete(dir, recursive: true);
            foreach (var entry in lockEntries)
            {
                lockfile.Projections.Remove(entry.Key);
            }

            if (lockEntries.Count > 0)
            {
                lockfile.Save(_layout.LockFile);
            }
        }

        foreach (var entry in lockEntries)
        {
            changes.Add($"prune lockfile entry {entry.Value.Skill}/{entry.Value.Target}");
        }

        var note = lockEntries.Count > 0
            ? "Generated sections already written into shared files (AGENTS.md etc.) are not removed automatically; run 'agent sync' or remove them by hand."
            : null;

        return new AuthoringResult(AuthoringStatus.Ok, changes, Array.Empty<ValidationMessage>(), dryRun, note, RecommendSync: lockEntries.Count > 0);
    }

    private bool TryRejectUnknownTargets(IReadOnlyList<string>? targets, out AuthoringResult? failure)
    {
        failure = null;
        if (targets is null)
        {
            return false;
        }

        var unknown = targets.Where(t => !TargetIds.IsKnown(t)).ToList();
        if (unknown.Count == 0)
        {
            return false;
        }

        failure = AuthoringResult.Fail(AuthoringStatus.InvalidUsage,
            $"Unknown target(s): {string.Join(", ", unknown)}. Known targets: {string.Join(", ", TargetIds.Ordered)}.");
        return true;
    }

    /// <summary>Re-loads + validates the workspace, attaching any errors and downgrading status to Invalid if needed.</summary>
    private AuthoringResult Validated(AuthoringStatus status, IReadOnlyList<string> changes, bool recommendSync)
    {
        var workspace = WorkspaceLoader.Load(_layout.RepoRoot);
        var errors = workspace.Validation.Messages.Where(m => m.Severity == ValidationSeverity.Error).ToList();
        var finalStatus = errors.Count > 0 ? AuthoringStatus.Invalid : status;
        return new AuthoringResult(finalStatus, changes, errors, DryRun: false, RecommendSync: recommendSync);
    }
}
