using AgentSync.Core.Adapters;
using AgentSync.Core.Authoring;
using AgentSync.Core.Configuration;

namespace AgentSync.Core.Import;

/// <summary>Options for <see cref="SkillImporter"/> (mirrors the <c>agent import skill</c> flags).</summary>
public sealed record SkillImportOptions(
    string? Id = null,
    string? Name = null,
    IReadOnlyList<string>? Targets = null,
    bool Force = false,
    bool DryRun = false);

/// <summary>
/// Imports a single skill (a skill folder or a standalone <c>SKILL.md</c>) into a
/// canonical skill under <c>.agent/skills/&lt;id&gt;/</c>. Pure plan-then-apply: a
/// dry-run takes the same path minus the writes.
/// </summary>
public sealed class SkillImporter
{
    private readonly RepoLayout _layout;

    public SkillImporter(string repoRoot) => _layout = new RepoLayout(repoRoot);

    public ImportReport Import(string rawPath, SkillImportOptions options)
    {
        ImportSource source;
        try
        {
            source = SourceDetector.Detect(_layout.RepoRoot, rawPath);
        }
        catch (RepoPathException ex)
        {
            return ImportReport.Failure(ImportStatus.UnsafePath, ex.Message, options.DryRun);
        }

        // Validate any requested target ids up front.
        if (options.Targets is { Count: > 0 })
        {
            var unknown = options.Targets.Where(t => !TargetIds.IsKnown(t)).ToList();
            if (unknown.Count > 0)
            {
                return ImportReport.Failure(
                    ImportStatus.InvalidSource,
                    $"Unknown target(s): {string.Join(", ", unknown)}. Known targets: {string.Join(", ", TargetIds.Ordered)}.",
                    options.DryRun);
            }
        }

        switch (source.Kind)
        {
            case ImportSourceKind.SkillFolder:
            case ImportSourceKind.SkillFile:
                break;
            case ImportSourceKind.Missing:
                return ImportReport.Failure(ImportStatus.SourceNotFound, source.Reason!, options.DryRun);
            case ImportSourceKind.SkillsRoot:
                return ImportReport.Failure(
                    ImportStatus.InvalidSource,
                    $"'{source.RelativePath}' is a skills root containing many skills. Use 'agent import agent {source.RelativePath}' to import them all.",
                    options.DryRun);
            default:
                return ImportReport.Failure(
                    ImportStatus.InvalidSource,
                    $"'{source.RelativePath}' is not a skill source. Use 'agent import agent' for instruction files. ({source.Reason})",
                    options.DryRun);
        }

        ParsedSkill parsed;
        try
        {
            parsed = SkillFolderReader.Read(source);
        }
        catch (ConfigParseException ex)
        {
            return ImportReport.Failure(ImportStatus.Problem, $"{source.RelativePath}: {ex.Message}", options.DryRun);
        }
        catch (FileNotFoundException ex)
        {
            return ImportReport.Failure(ImportStatus.Problem, ex.Message, options.DryRun);
        }

        var draft = BuildDraft(parsed, source, options, out var idError);
        if (draft is null)
        {
            return ImportReport.Failure(ImportStatus.InvalidSource, idError!, options.DryRun);
        }

        return ApplyOne(draft, options);
    }

    /// <summary>
    /// Builds a draft from a parsed skill source. Used directly by the skill-folder
    /// branch and reused by <c>import agent</c> when delegating skill folders.
    /// </summary>
    internal SkillDraft? BuildDraft(ParsedSkill parsed, ImportSource source, SkillImportOptions options, out string? idError)
    {
        idError = null;

        var id = options.Id ?? parsed.IdCandidate;
        if (id is null)
        {
            idError = $"Could not infer a valid skill id from '{source.RelativePath}'. Pass --id <skill-id>.";
            return null;
        }

        if (!IdInference.IsValid(id))
        {
            idError = $"Skill id '{id}' is not valid (must be lowercase kebab-case). Pass a valid --id.";
            return null;
        }

        var name = FirstNonEmpty(options.Name, parsed.Manifest.Name) ?? TitleCase(id);
        var description = FirstNonEmpty(parsed.Manifest.Description, InferDescription(parsed.Body)) ?? string.Empty;
        var version = FirstNonEmpty(parsed.Manifest.Version) ?? "0.1.0";
        var targets = options.Targets is { Count: > 0 } ? options.Targets : TargetIds.Ordered;
        var body = SkillContent.StripRedundantHeading(parsed.Body, name);

        return new SkillDraft(id, name, description, version, body, targets, source.RelativePath);
    }

    /// <summary>Plans and (unless dry-run) writes a single draft, returning a one-item report.</summary>
    internal ImportReport ApplyOne(SkillDraft draft, SkillImportOptions options)
    {
        var item = Plan(draft, options.Force);
        var items = new[] { item };

        if (item.Action == ImportAction.Skip)
        {
            return new ImportReport(ImportStatus.Problem, items, options.DryRun, item.Note);
        }

        if (!options.DryRun)
        {
            var manifestYaml = SkillFiles.RenderManifestYaml(draft.Id, draft.Name, draft.Description, draft.Version, draft.Targets);
            SkillFiles.Write(_layout, draft.Id, manifestYaml, draft.Body);
        }

        var status = item.HasValidationErrors ? ImportStatus.Problem : ImportStatus.Ok;
        return new ImportReport(status, items, options.DryRun);
    }

    private ImportItem Plan(SkillDraft draft, bool force)
    {
        var dir = SkillFiles.SkillDir(_layout, draft.Id);
        var exists = Directory.Exists(dir);
        var skillYaml = _layout.Relative(Path.Combine(dir, "skill.yaml"));
        var skillMd = _layout.Relative(Path.Combine(dir, "SKILL.md"));
        var validation = DraftValidation.Validate(draft).Messages;

        ImportAction action;
        string? note = null;
        if (!exists)
        {
            action = ImportAction.Create;
        }
        else if (force)
        {
            action = ImportAction.Overwrite;
        }
        else
        {
            action = ImportAction.Skip;
            note = $"Skill '{draft.Id}' already exists. Use --force to overwrite.";
        }

        return new ImportItem(draft.Id, draft.Name, draft.Description, skillYaml, skillMd, action, validation, draft.SourceRelativePath, note);
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? InferDescription(string body)
    {
        foreach (var rawLine in body.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            return line;
        }

        return null;
    }

    private static string TitleCase(string id)
        => string.Join(' ', id.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
