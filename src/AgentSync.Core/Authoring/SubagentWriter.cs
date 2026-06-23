using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;
using AgentSync.Core.Import;
using AgentSync.Core.Projections;
using AgentSync.Core.Subagents;

namespace AgentSync.Core.Authoring;

/// <summary>
/// Creates, edits, and deletes canonical sub-agents under <c>.agent/agents/&lt;id&gt;/</c>.
/// All writes go through <see cref="SubagentFiles"/> for deterministic output; deletes prune
/// the sub-agent lockfile so projections do not become orphans.
/// </summary>
public sealed class SubagentWriter
{
    private const string Target = "claude_agent";
    private const string DefaultBody = "## Role\n\nDescribe what this sub-agent does and when to delegate to it.\n";

    private readonly RepoLayout _layout;

    public SubagentWriter(string repoRoot) => _layout = new RepoLayout(repoRoot);

    public AuthoringResult Add(string id, string? name, string? description, string? model, string? color, IReadOnlyList<string>? tools)
    {
        if (!IdInference.IsValid(id))
        {
            return AuthoringResult.Fail(AuthoringStatus.InvalidUsage,
                $"Sub-agent id '{id}' must be lowercase kebab-case (a-z, 0-9, hyphens).");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return AuthoringResult.Fail(AuthoringStatus.InvalidUsage, "subagent add requires --description.");
        }

        if (SubagentFiles.Exists(_layout, id))
        {
            return AuthoringResult.Fail(AuthoringStatus.Conflict,
                $"Sub-agent '{id}' already exists. Use 'agent subagent edit {id}' to change it.");
        }

        var yaml = SubagentFiles.RenderManifestYaml(
            id,
            string.IsNullOrWhiteSpace(name) ? id : name!.Trim(),
            description!.Trim(),
            model?.Trim(),
            string.IsNullOrWhiteSpace(color) ? null : color!.Trim(),
            tools ?? Array.Empty<string>());
        SubagentFiles.Write(_layout, id, yaml, DefaultBody);

        return new AuthoringResult(AuthoringStatus.Ok, new[]
        {
            $"created {_layout.Relative(Path.Combine(SubagentFiles.AgentDir(_layout, id), "agent.yaml"))}",
            $"created {_layout.Relative(Path.Combine(SubagentFiles.AgentDir(_layout, id), "AGENT.md"))}",
        }, Array.Empty<ValidationMessage>(), RecommendSync: true);
    }

    public AuthoringResult Edit(
        string id,
        string? name,
        string? description,
        string? model,
        string? color,
        string? bodyFile,
        IReadOnlyList<string>? tools)
    {
        var dir = SubagentFiles.AgentDir(_layout, id);
        if (!Directory.Exists(dir))
        {
            return AuthoringResult.Fail(AuthoringStatus.NotFound, $"Sub-agent '{id}' does not exist.");
        }

        SubagentManifest manifest;
        try
        {
            var manifestPath = Path.Combine(dir, "agent.yaml");
            manifest = File.Exists(manifestPath) ? SubagentManifest.Parse(File.ReadAllText(manifestPath)) : new SubagentManifest();
        }
        catch (Configuration.ConfigParseException ex)
        {
            return AuthoringResult.Fail(AuthoringStatus.Invalid, $"{id}/agent.yaml: {ex.Message}");
        }

        manifest.Id = id;
        var body = File.Exists(Path.Combine(dir, "AGENT.md")) ? File.ReadAllText(Path.Combine(dir, "AGENT.md")) : string.Empty;
        var changes = new List<string>();

        if (name is not null) { manifest.Name = name.Trim(); changes.Add($"name = \"{manifest.Name}\""); }
        if (description is not null) { manifest.Description = description.Trim(); changes.Add($"description = \"{manifest.Description}\""); }
        if (model is not null) { manifest.Model = model.Trim().Length == 0 ? null : model.Trim(); changes.Add($"model = {manifest.Model ?? "(none)"}"); }
        if (color is not null) { manifest.Color = color.Trim().Length == 0 ? null : color.Trim(); changes.Add($"color = {manifest.Color ?? "(none)"}"); }
        if (tools is not null) { manifest.Tools = tools.Select(t => t.Trim()).Where(t => t.Length > 0).ToList(); changes.Add($"tools = {string.Join(", ", manifest.Tools)}"); }

        if (bodyFile is not null)
        {
            var absolute = BodyFile.Resolve(_layout.RepoRoot, bodyFile);
            if (!File.Exists(absolute))
            {
                return AuthoringResult.Fail(AuthoringStatus.InvalidUsage, $"Body file '{bodyFile}' does not exist.");
            }

            body = SkillContent.StripRedundantHeading(File.ReadAllText(absolute), manifest.Name ?? id);
            changes.Add($"body <- {bodyFile}");
        }

        if (changes.Count == 0)
        {
            return AuthoringResult.Fail(AuthoringStatus.InvalidUsage,
                "Nothing to edit. Pass --name, --description, --model, --color, --tool, or --body-file.");
        }

        var yaml = SubagentFiles.RenderManifestYaml(
            id,
            string.IsNullOrWhiteSpace(manifest.Name) ? id : manifest.Name!,
            manifest.Description ?? string.Empty,
            manifest.Model,
            manifest.Color,
            manifest.Tools);
        SubagentFiles.Write(_layout, id, yaml, body);

        return new AuthoringResult(AuthoringStatus.Ok, changes, Array.Empty<ValidationMessage>(), RecommendSync: true);
    }

    public AuthoringResult Delete(string id, bool force, bool dryRun)
    {
        var dir = SubagentFiles.AgentDir(_layout, id);
        if (!Directory.Exists(dir))
        {
            return AuthoringResult.Fail(AuthoringStatus.NotFound, $"Sub-agent '{id}' does not exist.");
        }

        var lockfile = Lockfile.Load(_layout.AgentsLockFile);
        var entries = lockfile.Projections
            .Where(kv => string.Equals(kv.Value.Target, Target, StringComparison.Ordinal)
                && string.Equals(kv.Value.Skill, id, StringComparison.Ordinal))
            .ToList();

        if (entries.Count > 0 && !force)
        {
            return new AuthoringResult(
                AuthoringStatus.Blocked,
                entries.Select(e => $"projection {e.Value.Path}").ToList(),
                Array.Empty<ValidationMessage>(),
                dryRun,
                $"Sub-agent '{id}' has {entries.Count} projection(s). Re-run with --force to delete it and prune its lockfile entries; the generated file under .claude/agents/ remains and should be removed by hand.");
        }

        var changes = new List<string> { $"delete {_layout.Relative(dir)}/" };
        foreach (var e in entries)
        {
            changes.Add($"prune lockfile entry {e.Value.Skill}/{e.Value.Target}");
        }

        if (dryRun)
        {
            return new AuthoringResult(AuthoringStatus.Ok, changes, Array.Empty<ValidationMessage>(), DryRun: true);
        }

        Directory.Delete(dir, recursive: true);
        if (entries.Count > 0)
        {
            foreach (var e in entries)
            {
                lockfile.Projections.Remove(e.Key);
            }

            lockfile.Save(_layout.AgentsLockFile);
        }

        var note = entries.Count > 0
            ? "The generated file under .claude/agents/ is not removed automatically; delete it by hand."
            : null;
        return new AuthoringResult(AuthoringStatus.Ok, changes, Array.Empty<ValidationMessage>(), dryRun, note, RecommendSync: entries.Count > 0);
    }
}
